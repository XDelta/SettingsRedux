using System;
using Elements.Core;
using FrooxEngine;
using FrooxEngine.UIX;
using static FrooxEngine.LocaleHelper;
using HarmonyLib;
using ResoniteModLoader;
using System.Collections.Generic;
using FrooxEngine.CommonAvatar;
using System.Reflection;
using Elements.Quantity;

namespace SettingsRedux;

public class SettingsRedux : ResoniteMod {
	public override string Name => "Settings Redux";
	public override string Author => "Delta";
	public override string Version => "2.1.0";
	public override string Link => "https://github.com/XDelta/SettingsRedux";

	[AutoRegisterConfigKey]
	private static readonly ModConfigurationKey<bool> enableSettingReplacement = new("enableSettingReplacement", "Replace the default Resonite settings page", () => true);

	[AutoRegisterConfigKey]
	private static readonly ModConfigurationKey<bool> useLinearSliders = new("useLinearSliders", "Use linear sliders instead of exponential ones (Requires changing tabs)", () => false);

	private static ModConfiguration Config;

	public override void OnEngineInit() {
		Config = GetConfiguration();
		Config.Save(true);
		Harmony harmony = new("net.deltawolf.SettingsRedux");
		harmony.PatchAll();
		Config.OnThisConfigurationChanged += delegate { SettingsDialog_OnAttach_Patch.SetUIState(); };
	}

	public enum Tabs {
		Audio,
		Controls,
		Video,
		Network,
		Misc
	}

	[HarmonyPatch(typeof(SettingsDialog), "OnAttach")]
	class SettingsDialog_OnAttach_Patch {

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
		private static SettingsDialog settingsDialog;
		private static SlideSwapRegion _slideSwap;
		private static SyncRef<QuantityTextEditorParser<Distance>> _heightField;
		private static Sync<bool> _useImperial;
		private static MethodInfo _OnSaveMethod;

		private static Tabs ActiveTab = Tabs.Misc;
		private static AudioSettingSync audioSettingSync;
		private static readonly float DefaultUiScale = 36f;

		private static Button b1;
		private static Button b2;
		private static Button b3;
		private static Button b4;
		private static Button b5;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

		//Nested patch
		[HarmonyPatch(typeof(Userspace), "OnCommonUpdate")]
		class TabSwitch_OnCommonUpdate {
			public static void Postfix() {
				if (b5 != null) {
					b1.SetColors(RadiantUI_Constants.BUTTON_COLOR);
					b2.SetColors(RadiantUI_Constants.BUTTON_COLOR);
					b3.SetColors(RadiantUI_Constants.BUTTON_COLOR);
					b4.SetColors(RadiantUI_Constants.BUTTON_COLOR);
					b5.SetColors(RadiantUI_Constants.BUTTON_COLOR);
					switch (ActiveTab) {
						case Tabs.Audio:
							b1.SetColors(RadiantUI_Constants.HIGHLIGHT_COLOR);
							break;
						case Tabs.Controls:
							b2.SetColors(RadiantUI_Constants.HIGHLIGHT_COLOR);
							break;
						case Tabs.Video:
							b3.SetColors(RadiantUI_Constants.HIGHLIGHT_COLOR);
							break;
						case Tabs.Network:
							b4.SetColors(RadiantUI_Constants.HIGHLIGHT_COLOR);
							break;
						case Tabs.Misc:
							b5.SetColors(RadiantUI_Constants.HIGHLIGHT_COLOR);
							break;
						default:
							break;
					}
				}
			}
		}
		//TODO Don't setup the original at all?, for now it is just hidden so you can toggle between the two on the fly
		/*public static bool Prefix(SettingsDialog __instance) {
			return false; //dont run rest of method
		}*/
		public static void Postfix(SettingsDialog __instance, SyncRef<QuantityTextEditorParser<Distance>> ____heightField, Sync<bool> ____useImperial) {
			__instance.Slot.AttachComponent<DuplicateBlock>(true, null);
			settingsDialog = __instance;
			_heightField = ____heightField;
			_useImperial = ____useImperial;
			_OnSaveMethod = AccessTools.FirstMethod(typeof(SettingsDialog), (methodInfo) => methodInfo.Name == "OnSaveSettings");

			_useImperial.SyncWithSetting(InputInterface.IMPERIAL_UNITS_SETTING, SettingSync.LocalChange.UpdateSetting);

			GenerateHeader();
			SetUIState();
			GenerateUi(Tabs.Audio); //Set and generate the Audio page
		}

		public static void SetUIState() {
			//TODO find SettingsRedux slots by tag? ensure that the correct slots are enabled/disabled
			if (Config.GetValue(enableSettingReplacement)) {
				settingsDialog.Slot[0].ActiveSelf = false; //Left
				settingsDialog.Slot[1].ActiveSelf = false; //Center
				settingsDialog.Slot[2].ActiveSelf = false; //Right
				if (settingsDialog.Slot.ChildrenCount == 5) {
					settingsDialog.Slot[3].ActiveSelf = true; //Header tabs
					settingsDialog.Slot[4].ActiveSelf = true; //Setting pages
				}
			} else {
				settingsDialog.Slot[0].ActiveSelf = true;
				settingsDialog.Slot[1].ActiveSelf = true;
				settingsDialog.Slot[2].ActiveSelf = true;
				if (settingsDialog.Slot.ChildrenCount == 5) {
					settingsDialog.Slot[3].ActiveSelf = false;
					settingsDialog.Slot[4].ActiveSelf = false;
				}
			}
		}

		private static void GenerateHeader() {
			UIBuilder ui = new(settingsDialog.Slot, null);
			RadiantUI_Constants.SetupDefaultStyle(ui, false);
			ui.SplitVertically(0.06f, out RectTransform top, out RectTransform bottom, 0.01f);
			ui.NestInto(top);
			ui.HorizontalLayout(10f);

			Button tabAudio = ui.Button("Settings.Audio.Header".AsLocaleKey(continuous: true, arguments: null)); //"Audio"
			tabAudio.LocalPressed += TabAudio;
			Button tabControl = ui.Button("Controls");
			tabControl.LocalPressed += TabControls;
			Button tabVideo = ui.Button("Video");
			tabVideo.LocalPressed += TabVideo;
			Button tabNetwork = ui.Button("Network");
			tabNetwork.LocalPressed += TabNetwork;
			Button tabMisc = ui.Button("Misc");
			tabMisc.LocalPressed += TabMisc;

			b1 = tabAudio;
			b2 = tabControl;
			b3 = tabVideo;
			b4 = tabNetwork;
			b5 = tabMisc;

			ui.NestOut();
			_slideSwap = bottom.Slot.AttachComponent<SlideSwapRegion>();
			audioSettingSync = ui.Current.AttachComponent<AudioSettingSync>();
		}

		private static void TabAudio(IButton button, ButtonEventData data) { GenerateUi(Tabs.Audio); }
		private static void TabControls(IButton button, ButtonEventData data) { GenerateUi(Tabs.Controls); }
		private static void TabVideo(IButton button, ButtonEventData data) { GenerateUi(Tabs.Video); }
		private static void TabNetwork(IButton button, ButtonEventData data) { GenerateUi(Tabs.Network); }
		private static void TabMisc(IButton button, ButtonEventData data) { GenerateUi(Tabs.Misc); }

		public static void GenerateUi(Tabs tab) {
			Msg("GenerateUi for tab: " + tab.ToString());
			SlideSwapRegion.Slide slide;
			slide = SlideVec(tab);
			if (tab == ActiveTab) { return; } /*Set tab is already active, don't generate anything new*/
			UIBuilder ui = _slideSwap.Swap(slide, 0.5f);
			UpdateActiveTab(tab);

			RadiantUI_Constants.SetupDefaultStyle(ui, false);

			List<RectTransform> list = ui.SplitHorizontally(0.1f, 0.8f, 0.1f);
			ui.NestInto(list[1]);

			RadiantUI_Constants.SetupDefaultStyle(ui, false);

			ui.ScrollArea(new Alignment?(Alignment.TopCenter));
			ui.VerticalLayout(4f, 0f, null);
			ui.FitContent(SizeFit.Disabled, SizeFit.PreferredSize);
			ui.Style.MinHeight = DefaultUiScale;

			switch (tab) {
				case Tabs.Audio:
					AddHeaderText(ui, "Settings.Audio.Header");
					AddAudioDriveSlider(ui, "Settings.Audio.Master", audioSettingSync.MasterVolume, "<b>{0}</b>");
					AddAudioSlider(ui, "Settings.Audio.SoundEffects", AudioTypeGroup.SoundEffect);
					AddAudioSlider(ui, "Settings.Audio.Multimedia", AudioTypeGroup.Multimedia);
					AddAudioSlider(ui, "Settings.Audio.Voice", AudioTypeGroup.Voice);
					AddAudioSlider(ui, "Settings.Audio.UI", AudioTypeGroup.UI);
					AddAudioDriveSlider(ui, "Settings.Audio.WhisperVoiceVolume", audioSettingSync.WhisperVoiceVolume);
					AddCheckboxDrive(ui, "Settings.Audio.DisableVoiceNormalization", audioSettingSync.DisableNormalization);
					AddAudioDriveSlider(ui, "Settings.Audio.NormzliationThreshold", audioSettingSync.NormalizationThreshold);
					AddCheckboxDrive(ui, "Settings.Audio.NoiseSupression", audioSettingSync.NoiseSupression);
					AddAudioDriveSlider(ui, "Settings.Audio.NoiseGateThreshold", audioSettingSync.NoiseGateThreshold);
					AddButton(ui, "Settings.Audio.SelectInputDevice", new ButtonEventHandler(OnSelectAudioInputDevice), 2f);
					AddButton(ui, "Settings.Audio.SelectOutputDevice", new ButtonEventHandler(OnSelectAudioOutputDevice), 2f);
					AddButton(ui, "Settings.Save", new ButtonEventHandler(OnSaveSettings));
					break;
				case Tabs.Controls:
					AddHeaderText(ui, "Height");
					AddQuantityTextEditorField(ui, "Settings.Height", InputInterface.HEIGHT_SETTING);
					ui.HorizontalLayout(20f);
					ui.ValueRadio<bool>("Settings.Metric".AsLocaleKey(continuous: true, arguments: null), _useImperial, false);
					ui.ValueRadio<bool>("Settings.Imperial".AsLocaleKey(continuous: true, arguments: null), _useImperial, true);

					//Background for radio button
					var image = ui.CurrentRect.Slot.AttachComponent<Image>();
					image.Tint.Value = RadiantUI_Constants.Dark.PURPLE.SetA(0.5f);
					ui.NestOut();

					AddHeaderText(ui, "Movement");
					UIBuilder uiEnum = ui;
					ValueField<Chirality> primaryHand = uiEnum.CurrentRect.Slot.AttachComponent<ValueField<Chirality>>(true, null);
					ui.HorizontalElementWithLabel<EnumMemberEditor>("Settings.PrimaryController".AsLocaleKey(continuous: true, arguments: null), 0.7f, () => ui.EnumMemberEditor(primaryHand.Value, null), 0.01f);
					primaryHand.Value.Value = Chirality.Right;
					primaryHand.Value.SyncWithSetting(InputInterface.PRIMARY_HAND_SETTING, SettingSync.LocalChange.UpdateSetting);

					AddCheckbox(ui, "Settings.AllowStrafing", "Input.Strafe");
					AddCheckbox(ui, "Settings.UseHeadDirectionForMovement", "Input.UseHeadDirection");
					AddCheckbox(ui, "Settings.SmoothTurn", "Input.SmoothTurn.Enabled");
					AddCheckbox(ui, "Settings.SmoothTurnExclusiveMode", "Input.SmoothTurn.ExclusiveMode");

					AddFloatTextEditorField(ui, "Settings.SmoothTurnSpeed", "Input.SmoothTurn.Speed", 90f, 5f, 1440f);
					AddFloatTextEditorField(ui, "Settings.SnapTurnAngle", "Input.SnapTurn.Angle", 45f, 5f, 180f);
					AddFloatTextEditorField(ui, "Settings.NoclipSpeed", "Input.MovementSpeed", 15f, 1f, 200f);
					AddFloatTextEditorField(ui, "Settings.SpeedExponent", "Input.MovementExponent", 2f, 0.2f, 10f, 2, "F2");
					AddFloatTextEditorField(ui, "Settings.MoveThreshold", "Input.MoveThreshold", 0.15f, 0f, 0.9f, 2, "F2");

					AddHeaderText(ui, "Haptics");
					AddCheckbox(ui, "Settings.ControllerVibration", InputInterface.VIBRATION_ENABLED_SETTING);
					AddCheckbox(ui, "Settings.Haptics", InputInterface.HAPTICS_ENABLED_SETTING);

					AddHeaderText(ui, "Interaction");
					AddCheckbox(ui, "Settings.ShowInteractionHints", "Input.ShowHints");
					AddCheckbox(ui, "Settings.DisablePhysicalInteractions", "Input.DisablePhysicalInteractions"); // CommonTool.CurrentDisablePhysicalInteractions
					AddFloatTextEditorField(ui, "Settings.DoubleClickInterval", "Input.DoubleClickInterval", 0.75f, 0.25f, 5f, 2, "F2");
					AddCheckbox(ui, "Settings.EnableGestures", "Input.Gestures"); // CommonTool.CurrentGestures
					AddCheckbox(ui, "Settings.Hotswitching", InputInterface.HOTSWITCHING_SETTING);

					AddHeaderText(ui, "Settings.FullBody.Header");
					AddFloatTextEditorField(ui, "Settings.FullBody.FeetPositionSmoothing", AvatarObjectSlot.SmoothingSettingName(BodyNode.LeftFoot, false), -1f, -1f, 60f, 1, "F1");
					AddFloatTextEditorField(ui, "Settings.FullBody.FeetRotationSmoothing", AvatarObjectSlot.SmoothingSettingName(BodyNode.LeftFoot, true), 20f, -1f, 60f, 1, "F1");
					AddFloatTextEditorField(ui, "Settings.FullBody.HipsPositionSmoothing", AvatarObjectSlot.SmoothingSettingName(BodyNode.Hips, false), -1f, -1f, 60f, 1, "F1");
					AddFloatTextEditorField(ui, "Settings.FullBody.HipsRotationSmoothing", AvatarObjectSlot.SmoothingSettingName(BodyNode.Hips, true), 20f, -1f, 60f, 1, "F1");

					AddHeaderText(ui, "Settings.LaserSmoothing.Header");
					AddFloatTextEditorField(ui, "Settings.LaserSmoothing.Speed", "Input.Laser.SmoothSpeed", 5f, 0.5f, 50f, 2, "F2");
					AddFloatTextEditorField(ui, "Settings.LaserSmoothing.ModulateStartAngle", "Input.Laser.SmoothModulateStartAngle", 2f, 0f, 180f, 2, "F2");
					AddFloatTextEditorField(ui, "Settings.LaserSmoothing.ModulateEndAngle", "Input.Laser.SmoothModulateEndAngle", 45f, 0f, 180f, 2, "F2");
					AddFloatTextEditorField(ui, "Settings.LaserSmoothing.ModulateExponent", "Input.Laser.SmoothModulateExp", 0.75f, 0.1f, 10f, 2, "F2");

					AddFloatTextEditorField(ui, "Settings.LaserSmoothing.ModulateSpeedMultiplier", "Input.Laser.SmoothModulateMultiplier", 8f, 0.1f, 100f, 2, "F2");
					AddFloatTextEditorField(ui, "Settings.LaserSmoothing.StickThreshold", "Input.Laser.StickThreshold", 0.2f, 0f, 2f, 2, "F2");
					AddButton(ui, "Settings.LaserSmoothing.Reset", new ButtonEventHandler(OnResetLaserSettings));
					AddCheckbox(ui, "Settings.Laser.ShowInDesktop", "Input.Laser.ShowInDesktop");
					break;

				case Tabs.Video:
					AddHeaderText(ui, "Settings.Dash.Header");
					AddFloatTextEditorField(ui, "Settings.Dash.Curvature", "Userspace.RadiantDash.Curvature", UserspaceRadiantDash.DEFAULT_CURVATURE, 0f, 1f, 2, "F2");//has slider
					AddSlider(ui, "Userspace.RadiantDash.Curvature", UserspaceRadiantDash.DEFAULT_CURVATURE, 0f, 1f);
					AddFloatTextEditorField(ui, "Settings.Dash.OpenCloseSpeed", "Userspace.RadiantDash.AnimationSpeed", UserspaceRadiantDash.DEFAULT_ANIMSPEED, 0.2f, float.PositiveInfinity, 2, "F2"); //max 10f
					AddFloatTextEditorField(ui, "Settings.Graphics.DesktopFOV", "Settings.Graphics.DesktopFOV", 60f, 10f, 179f, 2, "F2");//has slider
					AddSlider(ui, "Settings.Graphics.DesktopFOV", 60f, 10f, 120f);
					break;

				case Tabs.Network:
					AddHeaderText(ui, "Cloud");
					AddCheckbox(ui, "Settings.FetchIncompatibleSessions", "WorldAnnouncer.FetchIncompatibleSessions");
					AddCheckbox(ui, "Settings.DoNotSendReadStatus", "Cloud.Messaging.DoNotSendReadStatus");

					AddHeaderText(ui, "Connection");
					if (SteamNetworkManager.PreferSteam != null) {
						AddCheckbox(ui, "Settings.PreferSteamNetworking", "SteamNetworkingSockets.Prefer");
					}
					AddCheckbox(ui, "Settings.DisableLAN", "NetworkManager.Disable"); // NetworkManager.DISABLE_LAN_SETTING //SyncWithLocalVariable?
					AddIntTextEditorField(ui, "Settings.MaxConcurrentAssetTransfers", "Session.MaxConcurrentTransmitJobs", 4, 1, 128, 1);
					break;

				case Tabs.Misc:
					AddHeaderText(ui, "Language");
					AddButton(ui, "Settings.Locale.ChangeLanguage", new ButtonEventHandler(OnChangeLanguage), 2f);

					AddHeaderText(ui, "Screenshots");
					AddTextEditorField(ui, "Settings.AutosaveScreenshotPath", "Photos.AutoSavePath");

					AddHeaderText(ui, "Integrations");
					List<IConfigurable> configurables = new();

					foreach (IConfigurableInputDevice device in ui.World.Engine.InputInterface.GetDevices<IConfigurableInputDevice>(null)) {
						configurables.Add(device);
					}

					foreach (IConfigurable platformInterface in ui.World.Engine.PlatformInterface.GetConnectors<IConfigurable>(null)) {
						configurables.Add(platformInterface);
					}

					foreach (IConfigurable configurable in configurables) {
						List<ConfigurationProperty> properties = configurable.GetConfigurationProperties();

						if (properties != null && properties.Count > 0) {
							if (configurable.Name != null) {
								ui.Text(configurable.Name.AsLocaleKey("<b>{0}</b>", continuous: true, arguments: null));
							}

							foreach (ConfigurationProperty property in properties) {
								switch (property.PropertyType) {
									case PropertyType.Bool:
										Checkbox checkbox = ui.Checkbox(
											property.Name.AsLocaleKey(continuous: true, arguments: null)
										);
										checkbox.State.Value = (property.DefatulValue as bool?) ?? false; //DefatulValue hm
										checkbox.State.SyncWithLocalVariable(property.LocalVariablePath);
										break;
									case PropertyType.String:
										TextField textField = ui.HorizontalElementWithLabel<TextField>(
											property.Name.AsLocaleKey(continuous: true, arguments: null),
											0.35f,
											() => ui.TextField()
										);
										textField.Text.Content.Value = property.DefatulValue as string;
										textField.Text.Content.SyncWithLocalVariable(property.LocalVariablePath);
										break;
									case PropertyType.Float:
										FloatTextEditorParser floatField = ui.HorizontalElementWithLabel<FloatTextEditorParser>(
											property.Name.AsLocaleKey(continuous: true, arguments: null),
											0.7f,
											() => ui.FloatField()
										);
										floatField.ParsedValue.Value = (property.DefatulValue as float?) ?? 0.0f;
										floatField.ParsedValue.SyncWithLocalVariable(property.LocalVariablePath);
										break;
									default:
										ui.Text($"Unknown Property Type {property.PropertyType}");
										Warn($"Unknown Property Type {property.PropertyType}");
										break;
								}
							}
						}
					}

					AddHeaderText(ui, "Legacy");
					AddCheckbox(ui, "Settings.LegacyGripEquip", "Input.GripEquip");
					AddCheckbox(ui, "Settings.LegacyWorldSwitcher", "Userspace.WorldSwitcher.Enabled");

					AddHeaderText(ui, "Debug");
					AddCheckbox(ui, "Settings.DebugInputBinding", "Input.DebugInputBinding");
					break;

				default:
					AddHeaderText(ui, "Invalid Tab");
					Warn("Invalid Tab" + ActiveTab);
					break;
			}
			AddSpacer(ui);
			AltLineColor(list[1]);
			ui.NestOut();
		}

		public static void UpdateActiveTab(Tabs tab) {
			ActiveTab = tab;
		}

		public static void AltLineColor(RectTransform rect) {
			var currentLine = 0;
			foreach (var slot in rect.Slot.GetAllChildren()) {
				if (slot.Name == "Panel" && slot.Parent.Name == "Content") {
					if (currentLine % 2 == 1) {
						//odd numbered
						var image = slot.AttachComponent<Image>();
						image.Tint.Value = RadiantUI_Constants.Dark.PURPLE.SetA(0.5f);
					}
					currentLine += 1;
				}
			}
		}

		public static void AddSpacer(UIBuilder ui) {
			ui.Spacer(DefaultUiScale);
		}

		public static void AddHeaderText(UIBuilder ui, String label) {
			AddSpacer(ui);
			ui.Text(label.AsLocaleKey("<b>{0}</b>", continuous: true), true, Alignment.MiddleLeft);
			RectTransform sp = ui.Spacer(3f);
			sp.Slot.AttachComponent<Image>().Tint.Value = RadiantUI_Constants.Hero.PURPLE;
		}

		public static void AddButton(UIBuilder ui, string label, ButtonEventHandler method, float mult = 1f) {
			ui.Style.MinHeight = DefaultUiScale * mult;
			ui.Button(label.AsLocaleKey(continuous: true, arguments: null), method);
			ui.Style.MinHeight = DefaultUiScale;
		}

		public static void AddCheckbox(UIBuilder ui, String label, string path, SettingSync.LocalChange localChangeAction = SettingSync.LocalChange.UpdateSetting) {
			Checkbox checkbox = ui.Checkbox(label.AsLocaleKey(continuous: true, arguments: null));
			checkbox.State.SyncWithSetting(path, localChangeAction);
		}

		public static void AddCheckboxDrive(UIBuilder ui, String label, Sync<bool> driveSetting) {
			Checkbox checkbox = ui.Checkbox(label.AsLocaleKey(continuous: true, arguments: null));
			checkbox.State.DriveFrom(driveSetting, true);
		}

		public static void AddFloatTextEditorField(UIBuilder ui, string label, string path, float defaultValue, float minValue, float maxValue, int decimalPlaces = 0, string format = "F0", SettingSync.LocalChange localChangeAction = SettingSync.LocalChange.UpdateSetting) {
			FloatTextEditorParser floatTextEditorParser = ui.HorizontalElementWithLabel<FloatTextEditorParser>(label.AsLocaleKey(continuous: true, arguments: null), 0.7f, () => ui.FloatField(minValue, maxValue, decimalPlaces, format, true), 0.01f);
			floatTextEditorParser.ParsedValue.SyncWithSetting(path, localChangeAction);
			floatTextEditorParser.ParsedValue.Value = defaultValue;
		}

		public static void AddTextEditorField(UIBuilder ui, string label, string path, SettingSync.LocalChange localChangeAction = SettingSync.LocalChange.UpdateSetting) {
			ui.Text(label.AsLocaleKey(continuous: true, arguments: null));
			TextField textField = ui.TextField();
			textField.Text.Content.SyncWithSetting(path, localChangeAction);
		}

		public static void AddIntTextEditorField(UIBuilder ui, string label, string path, int defaultValue, int minValue, int maxValue, int valueIncrement = 0) {
			IntTextEditorParser intTextEditorParser = ui.HorizontalElementWithLabel<IntTextEditorParser>(label.AsLocaleKey(continuous: true, arguments: null), 0.7f, () => ui.IntegerField(minValue, maxValue, valueIncrement, true), 0.01f);
			intTextEditorParser.ParsedValue.Value = defaultValue;
			intTextEditorParser.ParsedValue.SyncWithLocalVariable(path);
		}

		public static void AddQuantityTextEditorField(UIBuilder ui, string label, string path) {
			QuantityTextEditorParser<Distance> quantityTextEditorParser = ui.HorizontalElementWithLabel<QuantityTextEditorParser<Distance>>(label.AsLocaleKey(continuous: true, arguments: null), 0.7f, () => ui.QuantityField<Distance>(new Distance(0.0), new Distance(2.2), false), 0.01f);
			quantityTextEditorParser.ParsedValue.SyncWithSetting(path, SettingSync.LocalChange.UpdateSetting);
			_heightField.Target = quantityTextEditorParser;
			quantityTextEditorParser.FormatNumber.Value = "F0";
			quantityTextEditorParser.FormatUnit.Value = "cm";
			quantityTextEditorParser.DefaultUnit.Value = "cm";
			quantityTextEditorParser.IgnoreOutOfRange.Value = true;
		}

		public static void AddSlider(UIBuilder ui, string path, float defaultValue, float min, float max) {
			Slider<float> slider = ui.Slider(ui.Style.MinHeight, defaultValue, min, max, false);
			slider.Value.SyncWithSetting(path, SettingSync.LocalChange.UpdateSetting);
		}

		public static void AddAudioSlider(UIBuilder ui, string localeString, AudioTypeGroup audioTypeGroup) {
			Text text = ui.Text(localeString);
			Slider<float> slider = ui.Slider(ui.Style.MinHeight);
			slider.Power.Value = (Config.GetValue(useLinearSliders) ? 1f : 0.5f);
			AudioTypeGroupVolumeSlider audioTypeGroupVolumeSlider = ui.Current.AttachComponent<AudioTypeGroupVolumeSlider>();
			audioTypeGroupVolumeSlider.Slider.Target = slider;
			audioTypeGroupVolumeSlider.Group.Value = audioTypeGroup;
			text.Content.DriveLocalized(localeString, null, arguments: new Dictionary<string, object> {
				{
					"n", slider.Value
				}
			});
		}

		public static void AddAudioDriveSlider(UIBuilder ui, string localeString, IField driveFromSource, string format = null) {
			Text text = ui.Text("");
			Slider<float> slider = ui.Slider(ui.Style.MinHeight);
			slider.Power.Value = (Config.GetValue(useLinearSliders) ? 1f : 0.5f);
			text.Content.DriveLocalized(localeString, format, arguments: new Dictionary<string, object> {
				{
					"n", slider.Value
				}
			});

			slider.Value.DriveFrom(driveFromSource, true);
		}

		public static SlideSwapRegion.Slide SlideVec(Tabs tab) {
			int num = tab.CompareTo(ActiveTab);
			if (num < 0) {
				return SlideSwapRegion.Slide.Right;
			} else if (num > 0) {
				return SlideSwapRegion.Slide.Left;
			} else {
				return SlideSwapRegion.Slide.None;
			}
		}


		[SyncMethod(typeof(Delegate))]
		private static void OnChangeLanguage(IButton button, ButtonEventData eventData) {
			settingsDialog.Slot.OpenModalOverlay(new float2(0.7f, 0.7f)).Slot.AttachComponent<LanguageSelection>();
		}

		[SyncMethod(typeof(Delegate))]
		private static void OnSelectAudioInputDevice(IButton button, ButtonEventData eventData) {
			settingsDialog.Slot.OpenModalOverlay(new float2(0.7f, 0.7f)).Slot.AttachComponent<AudioInputDeviceSelection>().SetupAsSettingSelection();
		}

		[SyncMethod(typeof(Delegate))]
		private static void OnSelectAudioOutputDevice(IButton button, ButtonEventData eventData) {
			settingsDialog.Slot.OpenModalOverlay(new float2(0.4f, 0.7f)).Slot.AttachComponent<AudioOutputDeviceSelection>().SetupAsSettingSelection();
		}

		[SyncMethod(typeof(Delegate))]
		private static void OnSaveSettings(IButton button, ButtonEventData eventData) {
			_OnSaveMethod.Invoke(settingsDialog, new object[] { button, eventData });
		}

		[SyncMethod(typeof(Delegate))]
		private static void OnResetLaserSettings(IButton button, ButtonEventData eventData) {
			Settings.WriteValue<float>("Input.Laser.SmoothSpeed", 5f);
			Settings.WriteValue<float>("Input.Laser.SmoothModulateStartAngle", 2f);
			Settings.WriteValue<float>("Input.Laser.SmoothModulateEndAngle", 45f);
			Settings.WriteValue<float>("Input.Laser.SmoothModulateExp", 0.75f);
			Settings.WriteValue<float>("Input.Laser.SmoothModulateMultiplier", 8f);
			Settings.WriteValue<float>("Input.Laser.StickThreshold", 0.2f);
		}
	}
}
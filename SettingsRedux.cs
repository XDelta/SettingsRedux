using System;
using BaseX;
using FrooxEngine;
using FrooxEngine.UIX;
using static FrooxEngine.LocaleHelper;
using HarmonyLib;
using NeosModLoader;
using System.Collections.Generic;
using FrooxEngine.CommonAvatar;
using System.Linq;
using QuantityX;
using System.Reflection;
using System.Threading.Tasks;

namespace SettingsRedux {
	public class SettingsRedux : NeosMod {
		public override string Name => "SettingsRedux";
		public override string Author => "XDelta";
		public override string Version => "1.0.0";
		public override string Link => "https://github.com/XDelta/SettingsRedux";

		[AutoRegisterConfigKey]
		private static readonly ModConfigurationKey<bool> enableSettingReplacement = new ModConfigurationKey<bool>("enableSettingReplacement", "Replace default Neos settings page", () => true);

		private static ModConfiguration Config;

		public override void OnEngineInit() {
			Config = GetConfiguration();
			Config.Save(true);
			Harmony harmony = new Harmony("tk.deltawolf.SettingsRedux");
			harmony.PatchAll();
			Config.OnThisConfigurationChanged += delegate { SettingsDialog_OnAttach_Patch.HideDefaultUi(); };
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

			private static SettingsDialog settingsDialog;
			private static SlideSwapRegion _slideSwap;
			private static SyncRef<QuantityTextEditorParser<Distance>> _heightField;
			private static Sync<bool> _useImperial;
			private static MethodInfo _OnSaveMethod;

			private static Tabs ActiveTab = Tabs.Audio;
			private static AudioSettingSync audioSettingSync;
			private static float DefaultUiScale = 28f;

			//private static SyncRefList<Button> _tabButtons; //.Add() always caused a NRE
			private static Button b1;
			private static Button b2;
			private static Button b3;
			private static Button b4;
			private static Button b5;

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
								b1.SetColors(new color(0, 0.29f, 0.78f));
								break;
							case Tabs.Controls:
								b2.SetColors(new color(0, 0.29f, 0.78f));
								break;
							case Tabs.Video:
								b3.SetColors(new color(0, 0.29f, 0.78f));
								break;
							case Tabs.Network:
								b4.SetColors(new color(0, 0.29f, 0.78f));
								break;
							case Tabs.Misc:
								b5.SetColors(new color(0, 0.29f, 0.78f));
								break;
							default:
								break;
						}
					}
				}
			}
			//TODO Don't setup the original at all?, for now it is just hidden so you can toggle between the two on the fly
			public static void Postfix(SettingsDialog __instance) {
				__instance.Slot.AttachComponent<DuplicateBlock>(true, null);
				settingsDialog = __instance;

				_heightField = AccessTools.Field(typeof(SettingsDialog), "_heightField").GetValue(settingsDialog) as SyncRef<QuantityTextEditorParser<Distance>>;
				_useImperial = AccessTools.Field(typeof(SettingsDialog), "_useImperial").GetValue(settingsDialog) as Sync<bool>;
				_OnSaveMethod = AccessTools.FirstMethod(typeof(SettingsDialog), (methodInfo) => methodInfo.Name == "OnSaveSettings");

				ActiveTab = Tabs.Misc;
				GenerateHeader();
				HideDefaultUi();
				GenerateUi(Tabs.Audio);
				
			}

				public static void HideDefaultUi() {
				if (Config.GetValue(enableSettingReplacement)) {
					settingsDialog.Slot[0].ActiveSelf = false;
					settingsDialog.Slot[1].ActiveSelf = false;
					if (settingsDialog.Slot.ChildrenCount == 4) {
						settingsDialog.Slot[2].ActiveSelf = true;
						settingsDialog.Slot[3].ActiveSelf = true;
					}
				} else {
					settingsDialog.Slot[0].ActiveSelf = true;
					settingsDialog.Slot[1].ActiveSelf = true;
					if (settingsDialog.Slot.ChildrenCount == 4) {
						settingsDialog.Slot[2].ActiveSelf = false;
						settingsDialog.Slot[3].ActiveSelf = false;
					}
				}
			}

			private static void GenerateHeader() {
				UIBuilder ui = new UIBuilder(settingsDialog.Slot, null);
				RadiantUI_Constants.SetupDefaultStyle(ui, false);
				RectTransform top;
				RectTransform bottom;
				ui.SplitVertically(0.05f, out top, out bottom, 0.02f);
				ui.NestInto(top);
				ui.HorizontalLayout(4);

				Button tabAudio = ui.Button("Audio");
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
				_slideSwap = bottom.Slot.AttachComponent<SlideSwapRegion>(true, null);
				audioSettingSync = ui.Current.AttachComponent<AudioSettingSync>(true, null);
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
				RectTransform top;
				RectTransform bottom;
				ui.SplitVertically(0.05f, out top, out bottom, 0.02f);
				ui.NestInto(top);
				//ui.TextField(tab.ToString()); //Label the tab you are currently on
				ui.NestOut();
				ui.NestInto(bottom);

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
						AddMasterAudioSlider(ui, "Settings.Audio.Master", audioSettingSync.MasterVolume);
						AddAudioSlider(ui, "Settings.Audio.SoundEffects", AudioTypeGroup.SoundEffect);
						AddAudioSlider(ui, "Settings.Audio.Multimedia", AudioTypeGroup.Multimedia);
						AddAudioSlider(ui, "Settings.Audio.Voice", AudioTypeGroup.Voice);
						AddAudioSlider(ui, "Settings.Audio.UI", AudioTypeGroup.UI);

						AddCheckboxDrive(ui, "Settings.Audio.DisableVoiceNormalization", audioSettingSync.DisableNormalization);
						AddCheckboxDrive(ui, "Settings.Audio.NoiseSupression", audioSettingSync.NoiseSupression);
						AddMasterAudioSlider(ui, "Settings.Audio.WhisperVoiceVolume", audioSettingSync.WhisperVoiceVolume);
						AddMasterAudioSlider(ui, "Settings.Audio.NormzliationThreshold", audioSettingSync.NormalizationThreshold);
						AddButton(ui, "Settings.Audio.SelectInputDevice", new ButtonEventHandler(OnSelectAudioInputDevice), 2f);
						AddButton(ui, "Settings.Audio.SelectOutputDevice", new ButtonEventHandler(OnSelectAudioOutputDevice), 2f);
						AddButton(ui, "Settings.Save", new ButtonEventHandler(OnSaveSettings));
						break;
					case Tabs.Controls:
						AddHeaderText(ui, "Height");
						AddQuantityTextEditorField(ui, "Settings.Height", "Input.User.Height");
						ui.HorizontalLayout(4f, 4f, null);//TODO change spacing to move apart
						ui.ValueRadio<bool>("Settings.Metric".AsLocaleKey(null, true, null), _useImperial, false);
						ui.ValueRadio<bool>("Settings.Imperial".AsLocaleKey(null, true, null), _useImperial, true);
						var image = ui.CurrentRect.Slot.AttachComponent<Image>();
						image.Tint.Value = new color(1, 1, 1, 0.2f);
						_useImperial.SyncWithSetting(InputInterface.IMPERIAL_UNITS_SETTING, SettingSync.LocalChange.UpdateSetting);
						ui.NestOut();

						AddHeaderText(ui, "Movement");
						UIBuilder uiEnum = ui;
						ValueField<Chirality> primaryHand = uiEnum.CurrentRect.Slot.AttachComponent<ValueField<Chirality>>(true, null);
						ui.HorizontalElementWithLabel<EnumMemberEditor>("Settings.PrimaryController".AsLocaleKey(null, true, null), 0.7f, () => ui.EnumMemberEditor(primaryHand.Value, null), 0.01f);
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
						//These have a state also passed to the button, not sure what it is used for or if it is required for it to work
						AddCheckbox(ui, "Settings.ControllerVibration", "Input.VibrationEnabled"); // InputInterface.VIBRATION_ENABLED_SETTING
						AddCheckbox(ui, "Settings.Haptics", "Input.HapticsEnabled"); // InputInterface.HAPTICS_ENABLED_SETTING

						AddHeaderText(ui, "Interaction");
						AddCheckbox(ui, "Settings.ShowInteractionHints", "Input.ShowHints");
						AddCheckbox(ui, "Settings.DisablePhysicalInteractions", "Input.DisablePhysicalInteractions"); // CommonTool.CurrentDisablePhysicalInteractions
						AddFloatTextEditorField(ui, "Settings.DoubleClickInterval", "Input.DoubleClickInterval", 0.75f, 0.25f, 5f, 2, "F2");
						AddCheckbox(ui, "Settings.EnableGestures", "Input.Gestures"); // CommonTool.CurrentGestures

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
						AddFloatTextEditorField(ui, "Settings.Dash.Curvature", "Userspace.RadiantDash.Curvature", 0.65f, 0f, 1f, 2, "F2");//has slider
						AddSlider(ui, "Userspace.RadiantDash.Curvature",0.65f, 0f, 1f);
						AddFloatTextEditorField(ui, "Settings.Dash.OpenCloseSpeed", "Userspace.RadiantDash.AnimationSpeed", 1.33f, 0.2f, float.PositiveInfinity, 2, "F2"); //max 10f
						AddFloatTextEditorField(ui, "Settings.Graphics.DesktopFOV", "Settings.Graphics.DesktopFOV", 60f, 10f, 179f, 2, "F2");//has slider
						AddSlider(ui, "Settings.Graphics.DesktopFOV", 60f, 10f, 120f);
						break;
					case Tabs.Network:
						AddHeaderText(ui,"Cloud");
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
						
						AddHeaderText(ui, "Tutorials");
						AddCheckbox(ui, "Settings.HideAllTutorials", "Tutorials.GLOBAL.Hide"); //TutorialManager.TUTORIALS_HIDE_SETTING
						AddButton(ui, "Settings.ResetAllTutorials", new ButtonEventHandler(OnResetTutorials), 2f);
						
						AddHeaderText(ui, "Legacy");
						AddCheckbox(ui, "Settings.LegacyGripEquip", "Input.GripEquip");
						AddCheckbox(ui, "Settings.LegacyWorldSwitcher", "Userspace.WorldSwitcher.Enabled");
						
						AddHeaderText(ui, "Debug");
						AddCheckbox(ui, "Settings.DebugInputBinding", "Input.DebugInputBinding");

						//AddHeaderText(ui, "");
						//TODO hardware integrations ex. Windows, vive, bhaptics
						//TODO Auto-save screenshot path
						break;
					default:
						AddHeaderText(ui, "Invalid Tab");
						Msg("Invalid Tab" + ActiveTab);
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
				var count = rect.Slot.ChildrenCount;
				foreach (var slot in rect.Slot.GetAllChildren()) {
					//Debug(slot.Name + ":" + currentLine);
					if (slot.Name == "Panel" && slot.Parent.Name == "Content") {
						if (currentLine % 2 == 1) {
							//odd numbered
							var image = slot.AttachComponent<Image>();
							image.Tint.Value = new color(1, 1, 1, 0.2f);
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
				ui.Text(label.AsLocaleKey("<b>{0}</b>", true, null), true, Alignment.MiddleLeft, true, null);
				RectTransform sp = ui.Spacer(3f);
				sp.Slot.AttachComponent<Image>().Tint.Value = new color(1f, 1f, 1f, 0.5f);
			}

			public static void AddButton(UIBuilder ui, string label, ButtonEventHandler method, float mult = 1f) {
				ui.Style.MinHeight = DefaultUiScale * mult;
				ui.Button(label.AsLocaleKey(null, true, null), method);
				ui.Style.MinHeight = DefaultUiScale;
			}

			public static void AddCheckbox(UIBuilder ui, String label, string path, SettingSync.LocalChange localChangeAction = SettingSync.LocalChange.UpdateSetting) {
				Checkbox checkbox = ui.Checkbox(label.AsLocaleKey(null, true, null), false, true, 4f);
				checkbox.State.SyncWithSetting(path, localChangeAction);
			}

			public static void AddCheckboxDrive(UIBuilder ui, String label, Sync<bool> driveSetting) {
				Checkbox checkbox = ui.Checkbox(label.AsLocaleKey(null, true, null), false, true, 4f);
				checkbox.State.DriveFrom(driveSetting, true, false, true);
			}

			public static void AddFloatTextEditorField(UIBuilder ui, string label, string path, float defaultValue, float minValue, float maxValue, int decimalPlaces = 0, string format = "F0", SettingSync.LocalChange localChangeAction = SettingSync.LocalChange.UpdateSetting) {
				FloatTextEditorParser floatTextEditorParser = ui.HorizontalElementWithLabel<FloatTextEditorParser>(label.AsLocaleKey(null, true, null), 0.7f, () => ui.FloatField(minValue, maxValue, decimalPlaces, format, true), 0.01f);
				floatTextEditorParser.ParsedValue.SyncWithSetting(path, localChangeAction);
				floatTextEditorParser.ParsedValue.Value = defaultValue;
			}

			public static void AddIntTextEditorField(UIBuilder ui, string label, string path, int defaultValue, int minValue, int maxValue, int valueIncrement = 0) {
				IntTextEditorParser intTextEditorParser = ui.HorizontalElementWithLabel<IntTextEditorParser>(label.AsLocaleKey(null, true, null), 0.7f, () => ui.IntegerField(minValue, maxValue, valueIncrement, true), 0.01f);
				intTextEditorParser.ParsedValue.Value = defaultValue;
				intTextEditorParser.ParsedValue.SyncWithLocalVariable(path);
			}

			public static void AddQuantityTextEditorField(UIBuilder ui, string label, string path) {
				QuantityTextEditorParser<Distance> quantityTextEditorParser = ui.HorizontalElementWithLabel<QuantityTextEditorParser<Distance>>(label.AsLocaleKey(null, true, null), 0.7f, () => ui.QuantityField<Distance>(new Distance(0.0), new Distance(2.2), false), 0.01f);
				quantityTextEditorParser.ParsedValue.SyncWithSetting(path, SettingSync.LocalChange.UpdateSetting);
				_heightField.Target = quantityTextEditorParser;
				quantityTextEditorParser.FormatNumber.Value = "F0";
				quantityTextEditorParser.FormatUnit.Value = "cm";
				quantityTextEditorParser.DefaultUnit.Value = "cm";
				quantityTextEditorParser.IgnoreOutOfRange.Value = true;
			}

			public static void AddMasterAudioSlider(UIBuilder ui, string localeString, IField driveFromSource) {
				Text text = ui.Text("", true, null, true, null);
				Slider<float> slider = ui.Slider(ui.Style.MinHeight, 0f, 0f, 1f, false);
				slider.Power.Value = 0.5f;
				//System.MissingMethodException: FrooxEngine.LocaleStringDriver FrooxEngine.LocaleHelper.DriveLocalized(FrooxEngine.IField`1<string>,string,string,System.ValueTuple`2<string, object>[])
				text.Content.DriveLocalized(localeString, "<b>{0}</b>", new ValueTuple<string, object>[] {
					new ValueTuple<string, object>("n", slider.Value)
				});
				
				slider.Value.DriveFrom(driveFromSource, true, false, true);
				ApplySliderStyle(slider);
			}

			public static void AddSlider(UIBuilder ui, string path, float defaultValue, float min, float max) {
				Slider<float> slider = ui.Slider(ui.Style.MinHeight, defaultValue, min, max, false);
				slider.Value.SyncWithSetting(path, SettingSync.LocalChange.UpdateSetting);
				ApplySliderStyle(slider);
			}

			public static void AddAudioSlider(UIBuilder ui, string localeString, AudioTypeGroup audioTypeGroup) {
				Text text2 = ui.Text(localeString, true, null, true, null);
				Slider<float> slider = ui.Slider(ui.Style.MinHeight, 0f, 0f, 1f, false);
				slider.Power.Value = 0.5f;
				AudioTypeGroupVolumeSlider audioTypeGroupVolumeSlider = ui.Current.AttachComponent<AudioTypeGroupVolumeSlider>(true, null);
				audioTypeGroupVolumeSlider.Slider.Target = slider;
				audioTypeGroupVolumeSlider.Group.Value = audioTypeGroup;
				text2.Content.DriveLocalized(localeString, null, new ValueTuple<string, object>[] {
					new ValueTuple<string, object>("n", slider.Value)
				});
				ApplySliderStyle(slider);
			}

			public static void ApplySliderStyle(Slider<float> slider) {
				Slot bg = slider.Slot[0];
				Slot handle = slider.Slot[1];
				//Msg("Handle name slot: " + handle.Name);
				ValueCopy<float2> vc=bg[0].AttachComponent<ValueCopy<float2>>(true, null);
				vc.Source.Value = handle[0].GetComponent<RectTransform>().AnchorMax.ReferenceID;
				vc.Target.Value = bg[0].GetComponent<RectTransform>().AnchorMax.ReferenceID;
				Slot img = bg.AddSlot("Image");
				RectTransform rect = img.AttachComponent<RectTransform>();
				rect.AnchorMin.Value = new float2(0, 0.5f);
				rect.AnchorMax.Value = new float2(1, 0.5f);
				rect.OffsetMin.Value = new float2(0, -5.5f);
				rect.OffsetMax.Value = new float2(0, 5.5f);
				Image img2 = img.AttachComponent<Image>();
				img2.Tint.Value = new color(1,1,1,0.2f);
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


			[SyncMethod]
			private static void OnChangeLanguage(IButton button, ButtonEventData eventData) {
				settingsDialog.Slot.OpenModalOverlay(new float2(0.7f, 0.7f), false).Slot.AttachComponent<LanguageSelection>(true, null);
			}
			[SyncMethod]
			private static void OnSelectAudioInputDevice(IButton button, ButtonEventData eventData) {
				settingsDialog.Slot.OpenModalOverlay(new float2(0.7f, 0.7f), false).Slot.AttachComponent<AudioInputDeviceSelection>(true, null).SetupAsSettingSelection();
			}
			[SyncMethod]
			private static void OnSelectAudioOutputDevice(IButton button, ButtonEventData eventData) {
				settingsDialog.Slot.OpenModalOverlay(new float2(0.4f, 0.7f), false).Slot.AttachComponent<AudioOutputDeviceSelection>(true, null).SetupAsSettingSelection();
			}

			[SyncMethod]
			private static void OnSaveSettings(IButton button, ButtonEventData eventData) {
				_OnSaveMethod.Invoke(settingsDialog, new object[] {button, eventData});
			}

			[SyncMethod]
			private static void OnResetTutorials(IButton button, ButtonEventData eventData) {
				Settings.ClearSettings(TutorialManager.TUTORIALS_ROOT);
			}

			[SyncMethod]
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
}
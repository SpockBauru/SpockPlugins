﻿// BepInEx
using HarmonyLib;
using Il2CppSystem;
using Il2CppSystem.IO;
using UnhollowerBaseLib;
using UnhollowerRuntimeLib;

// Unity
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// Game Specific
using Chara;
using CharaCustom;
using Il2CppSystem.Collections.Generic;

namespace IllusionPlugins
{
    public partial class RG_MaterialMod
    {
        internal class Hooks
        {
            // ================================================== Load/Reload Section ==================================================
            // Initialize Character
            [HarmonyPostfix]
            [HarmonyPatch(typeof(ChaControl), nameof(ChaControl.Initialize))]
            private static void ChaControlInitialize(ChaControl __instance)
            {
                // Initialize Character
                ChaControl chaControl = __instance;
                GameObject characterObject = chaControl.gameObject;
                string characterName = characterObject.name;
                ChaFile chaFile = chaControl.ChaFile;
                CharacterContent characterContent;

                if (!CharactersLoaded.ContainsKey(characterName))
                    CharactersLoaded.Add(characterName, new CharacterContent());
                characterContent = CharactersLoaded[characterName];
                characterContent.characterObject = characterObject;
                characterContent.chaControl = chaControl;
                characterContent.chafile = chaFile;
                characterContent.enableSetTextures = true;
                ResetAllClothes(characterName);

                // Chara Maker stuff
                GameObject charaCustom = GameObject.Find("CharaCustom");
                if (charaCustom == null) return;

                // Save when click on options toggle
                GameObject optionsToggleObject = GameObject.Find("tglOption");
                Toggle optionsToggle = optionsToggleObject.GetComponent<Toggle>();
                optionsToggle.onValueChanged.AddListener((UnityAction<bool>)delegate
                {
                    SavePluginData(characterContent, optionsToggle);
                });
            }

            private static void SavePluginData(CharacterContent characterContent, Toggle toggle)
            {
                if (!toggle.isOn) return;
                SaveCard(characterContent);
            }

            // Reload Character Prefix
            [HarmonyPrefix]
            [HarmonyPatch(typeof(ChaControl), nameof(ChaControl.Reload))]
            private static void ChaControlReloadPre(ChaControl __instance)
            {
                ChaControl chaControl = __instance;
                GameObject characterObject = chaControl.gameObject;
                string characterName = characterObject.name;
                ChaFile chaFile = chaControl.ChaFile;
                CharacterContent characterContent;

                if (!CharactersLoaded.ContainsKey(characterName))
                    CharactersLoaded.Add(characterName, new CharacterContent());
                characterContent = CharactersLoaded[characterName];
                characterContent.characterObject = characterObject;
                characterContent.chaControl = chaControl;
                characterContent.chafile = chaFile;

                // Something bad happens between the ChaControlReloadPre and the ChaControlReloadPost
                characterContent.enableSetTextures = false;

                // ==== Chara Maker Section ===
                GameObject customControl = GameObject.Find("CustomControl");
                if (customControl == null) return;
                // Don't reset clothes when in clothes menu
                Toggle clothesMainToggle = GameObject.Find("tglClothes").GetComponent<Toggle>();

                if (clothesMainToggle.isOn) return;
                ResetAllClothes(characterName);
            }

            // Reload Character Postfix
            [HarmonyPostfix]
            [HarmonyPatch(typeof(ChaControl), nameof(ChaControl.Reload))]
            private static void ChaControlReloadPost(ChaControl __instance)
            {
                ChaControl chaControl = __instance;
                GameObject characterObject = chaControl.gameObject;
                string characterName = characterObject.name;
                CharacterContent characterContent = CharactersLoaded[characterName];
                characterContent.enableSetTextures = true;

                // If not in the chara maker, just load card and set textures
                GameObject customControl = GameObject.Find("CustomControl");
                if (customControl == null)
                {
                    LoadCard(characterContent);
                    SetAllTextures(characterName);
                }
                else
                {
                    // Make clothes tab
                    CvsC_Clothes cvsC_Clothes = customControl.GetComponentInChildren<CvsC_Clothes>();
                    int kindIndex = cvsC_Clothes.SNo;
                    MakeClothesDropdown(characterContent, kindIndex);

                    // Don't load cards when in clothes menu
                    Toggle clothesMainToggle = GameObject.Find("tglClothes").GetComponent<Toggle>();
                    if (!clothesMainToggle.isOn)
                    {
                        LoadCard(characterContent);
                        SetAllTextures(characterName);
                    }
                }
            }

            // Get when change the coordinate type (outer, house, bath)
            [HarmonyPrefix]
            [HarmonyPatch(typeof(ChaControl), nameof(ChaControl.ChangeCoordinateType), typeof(ChaFileDefine.CoordinateType), typeof(bool), typeof(bool))]
            private static void ChangeCoordinatePre(ChaControl __instance, ChaFileDefine.CoordinateType type)
            {
                GameObject characterObject = __instance.gameObject;
                string characterName = characterObject.name;
                CharacterContent characterContent = CharactersLoaded[characterName];

                characterContent.currentCoordinate = type;

                // Chara Maker Section
                GameObject customControl = GameObject.Find("CustomControl");
                if (customControl == null) return;
                CvsC_Clothes cvsC_Clothes = customControl.GetComponentInChildren<CvsC_Clothes>();
                int kindIndex = cvsC_Clothes.SNo;
                if (clothesTab.isOn) MakeClothesDropdown(characterContent, kindIndex);
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(ChaControl), nameof(ChaControl.ChangeCoordinateType), typeof(ChaFileDefine.CoordinateType), typeof(bool), typeof(bool))]
            private static void ChangeCoordinatePost(ChaControl __instance, ChaFileDefine.CoordinateType type)
            {
                GameObject characterObject = __instance.gameObject;
                string characterName = characterObject.name;
                SetAllTextures(characterName);
            }


            // ================================================== Clothes Submenu ==================================================
            // Initializing clothes tab in Chara Maker
            [HarmonyPostfix]
            [HarmonyPatch(typeof(CvsC_Clothes), nameof(CvsC_Clothes.Initialize))]
            private static void StartClothesMenu(CvsC_Clothes __instance)
            {
                CvsC_Clothes cvsC_Clothes = __instance;
                GameObject characterObject = cvsC_Clothes.chaCtrl.gameObject;
                string characterName = characterObject.name;
                CharacterContent characterContent = CharactersLoaded[characterName];
                int kindIndex = cvsC_Clothes.SNo;

                // Create clothes Tab in chara maker: GET TAB TOGGLE AND WINDOW CONTENT!
                clothesSelectMenu = GameObject.Find("CharaCustom/CustomControl/CanvasSub/SettingWindow/WinClothes/DefaultWin/C_Clothes/SelectMenu");
                clothesSettingsGroup = GameObject.Find("CharaCustom/CustomControl/CanvasSub/SettingWindow/WinClothes/DefaultWin/C_Clothes/Setting");
                (clothesTab, clothesTabContent) = RG_MaterialModUI.CreateMakerTab(clothesSelectMenu, clothesSettingsGroup);

                clothesTab.onValueChanged.AddListener((UnityAction<bool>)Make);
                void Make(bool isOn)
                {
                    kindIndex = cvsC_Clothes.SNo;
                    if (isOn) MakeClothesDropdown(characterContent, kindIndex);
                }
                MakeClothesDropdown(characterContent, kindIndex);

                // Make when entering clothes section
                Toggle clothesMainToggle = GameObject.Find("tglClothes").GetComponent<Toggle>();
                GameObject settingWindow = GameObject.Find("CharaCustom/CustomControl/CanvasSub/SettingWindow");
                clothesMainToggle.onValueChanged.AddListener((UnityAction<bool>)Deselect);
                void Deselect(bool isOn)
                {
                    if (isOn)
                    {
                        if (clothesSelectMenu.GetComponentsInChildren<UI_ToggleEx>(false).Count > 5)
                            RG_MaterialModUI.ChangeWindowSize(502f, settingWindow);
                        else RG_MaterialModUI.ChangeWindowSize(428, settingWindow);
                        MakeClothesDropdown(characterContent, kindIndex);
                    }
                }

                // Save and Exit button when you edit a girl in-game
                Button saveAndExit = GameObject.Find("CharaCustom/CustomControl/CanvasMain/btnExit").GetComponent<Button>();
                saveAndExit.onClick.AddListener((UnityAction)delegate { SaveCard(characterContent); });
            }

            // Get when submenu tabs change
            [HarmonyPostfix]
            [HarmonyPatch(typeof(CvsC_Clothes), nameof(CvsC_Clothes.RestrictClothesMenu))]
            private static void ResizeClothesWindow()
            {
                // Make settings size bigger if there's more than 5 tabs
                GameObject settingWindow = GameObject.Find("CharaCustom/CustomControl/CanvasSub/SettingWindow");
                if (clothesSelectMenu.GetComponentsInChildren<UI_ToggleEx>(false).Count > 5) RG_MaterialModUI.ChangeWindowSize(502f, settingWindow);
                else RG_MaterialModUI.ChangeWindowSize(428f, settingWindow);
            }

            // Get when change the clothes in right submenu
            [HarmonyPrefix]
            [HarmonyPatch(typeof(ChaControl), nameof(ChaControl.ChangeClothes), typeof(int), typeof(int), typeof(bool))]
            private static void ClothesChangedPre(ChaControl __instance, int id, int kind)
            {
                Debug.Log("ChaControl.ChangeClothes: " + kind);
                GameObject characterObject = __instance.gameObject;
                string characterName = characterObject.name;
                CharacterContent characterContent = CharactersLoaded[characterName];
                characterContent.enableSetTextures = true;

                ResetKind(characterName, TextureDictionaries.clothesTextures, kind);
            }

            // Get when clothes material is updated
            [HarmonyPostfix]
            [HarmonyPatch(typeof(ChaControl), nameof(ChaControl.ChangeCustomClothes))]
            private static void MaterialChanged(ChaControl __instance, int kind)
            {
                GameObject characterObject = __instance.gameObject;
                string characterName = characterObject.name;
                CharacterContent characterContent = CharactersLoaded[characterName];

                if (!characterContent.enableSetTextures) return;

                //SetAllClothesTextures(characterName);
                SetKind(characterName, TextureDictionaries.clothesTextures, kind);
            }

            // Get when clothing or accessory type change
            [HarmonyPostfix]
            [HarmonyPatch(typeof(CvsC_Clothes), nameof(CvsC_Clothes.ChangeMenuFunc))]
            private static void PieceUpdated(CvsC_Clothes __instance)
            {
                Debug.Log("CvsC_Clothes.ChangeMenuFunc: " + __instance.SNo);
                Canvas winClothes = GameObject.Find("CharaCustom/CustomControl/CanvasSub/SettingWindow/WinClothes").GetComponent<Canvas>();
                //if (!winClothes.enabled) return;

                GameObject characterObject = __instance.chaCtrl.gameObject;
                string characterName = characterObject.name;
                CharacterContent characterContent = CharactersLoaded[characterName];
                ChaControl chaControl = characterContent.chaControl;
                characterContent.enableSetTextures = true;
                int kindIndex = __instance.SNo;

                if (winClothes.enabled) SetKind(characterName, TextureDictionaries.clothesTextures, kindIndex);
                if (clothesTab.isOn && winClothes.enabled)
                {
                    MakeClothesDropdown(characterContent, kindIndex);
                }

                // == Accessory Section ==
                Canvas winAccessory = GameObject.Find("CharaCustom/CustomControl/CanvasSub/SettingWindow/WinAccessory").GetComponent<Canvas>();
                GameObject accessoryObject = chaControl.ObjAccessory[kindIndex];

                if (winAccessory.enabled) MakeAccessoryDropdown(characterContent, kindIndex);
            }

            // ================================================== Accessory Submenu ==================================================
            // Initializing Accessory tab in Chara Maker
            [HarmonyPostfix]
            [HarmonyPatch(typeof(CvsA_Slot), nameof(CvsA_Slot.Initialize))]
            private static void StartAccessoryMenu(CvsA_Slot __instance)
            {
                CvsA_Slot cvsA_Slot = __instance;
                GameObject characterObject = cvsA_Slot.chaCtrl.gameObject;
                string characterName = characterObject.name;
                CharacterContent characterContent = CharactersLoaded[characterName];
                int kindIndex = cvsA_Slot.SNo;

                // Accessory clothes Tab in chara maker: GET TAB TOGGLE AND WINDOW CONTENT!
                accessorySelectMenu = GameObject.Find("CharaCustom/CustomControl/CanvasSub/SettingWindow/WinAccessory/A_Slot/SelectMenu");
                accessorySettingsGroup = GameObject.Find("CharaCustom/CustomControl/CanvasSub/SettingWindow/WinAccessory/A_Slot/Setting");
                (accessoryTab, accessoryTabContent) = RG_MaterialModUI.CreateMakerTab(accessorySelectMenu, accessorySettingsGroup);

                accessoryTab.onValueChanged.AddListener((UnityAction<bool>)Make);
                void Make(bool isOn)
                {
                    kindIndex = cvsA_Slot.SNo;
                    if (isOn) MakeAccessoryDropdown(characterContent, kindIndex);
                }

                // Make when entering clothes section
                Toggle accrssoryMainToggle = GameObject.Find("CharaCustom/CustomControl/CanvasMain/MainMenu/tglAccessory").GetComponent<Toggle>();
                GameObject settingWindow = GameObject.Find("CharaCustom/CustomControl/CanvasSub/SettingWindow");
                accrssoryMainToggle.onValueChanged.AddListener((UnityAction<bool>)Deselect);
                void Deselect(bool isOn)
                {
                    if (isOn)
                    {
                        if (accessorySelectMenu.GetComponentsInChildren<UI_ToggleEx>(false).Count > 5)
                             RG_MaterialModUI.ChangeWindowSize(502f, settingWindow);
                        else RG_MaterialModUI.ChangeWindowSize(428, settingWindow);
                        MakeAccessoryDropdown(characterContent, kindIndex);
                    }
                }
            }

            // Get when submenu tabs change
            [HarmonyPostfix]
            [HarmonyPatch(typeof(CvsA_Slot), nameof(CvsA_Slot.RestrictAcsMenu))]
            private static void ResizeAccessoryWindow()
            {
                // Make settings size bigger if there's more than 5 tabs
                GameObject settingWindow = GameObject.Find("CharaCustom/CustomControl/CanvasSub/SettingWindow");
                if (accessorySelectMenu.GetComponentsInChildren<UI_ToggleEx>(false).Count > 5) RG_MaterialModUI.ChangeWindowSize(502f, settingWindow);
                else RG_MaterialModUI.ChangeWindowSize(428f, settingWindow);
            }

            //Get when Accessory material is updated
            [HarmonyPostfix]
            [HarmonyPatch(typeof(ChaControl), nameof(ChaControl.ChangeAccessoryColor))]
            private static void AccessoryMaterialChanged(int slotNo, ChaControl __instance)
            {
                GameObject characterObject = __instance.gameObject;
                string characterName = characterObject.name;
                CharacterContent characterContent = CharactersLoaded[characterName];

                if (!characterContent.enableSetTextures) return;

                //SetAllClothesTextures(characterName);
                SetKind(characterName, TextureDictionaries.accessoryTextures, slotNo);
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(ChaControl), nameof(ChaControl.ChangeAccessoryParent))]
            private static void AccessoryChanged(int slotNo, ChaControl __instance)
            {
                GameObject characterObject = __instance.gameObject;
                string characterName = characterObject.name;
                CharacterContent characterContent = CharactersLoaded[characterName];
                characterContent.enableSetTextures = true;

                // If not in chara maker, just set accessories
                GameObject customControl = GameObject.Find("CustomControl");
                if (customControl != null)
                {
                    Toggle accessoryMainToggle = GameObject.Find("CharaCustom/CustomControl/CanvasMain/MainMenu/tglAccessory").GetComponent<Toggle>();
                    Toggle clothesMainToggle = GameObject.Find("tglClothes").GetComponent<Toggle>();
                    if (accessoryMainToggle.isOn && !clothesMainToggle.isOn)
                    {
                        ResetKind(characterName, TextureDictionaries.accessoryTextures, slotNo);
                    }
                    else SetKind(characterName, TextureDictionaries.accessoryTextures, slotNo);
                }
                else SetKind(characterName, TextureDictionaries.accessoryTextures, slotNo);
            }

            // ================================================== Hair Submenu ==================================================
            [HarmonyPostfix]
            [HarmonyPatch(typeof(CvsH_Hair), nameof(CvsH_Hair.Initialize))]
            private static void StartHairMenu(CvsH_Hair __instance)
            {
                Debug.Log("= CvsH_Hair.Initialize");

                CvsH_Hair cvsH_Hair = __instance;
                GameObject characterObject = cvsH_Hair.chaCtrl.gameObject;
                string characterName = characterObject.name;
                CharacterContent characterContent = CharactersLoaded[characterName];
                int kindIndex = cvsH_Hair.SNo;

                // Hair clothes Tab in chara maker: GET TAB TOGGLE AND WINDOW CONTENT!
                hairSelectMenu = GameObject.Find("CharaCustom/CustomControl/CanvasSub/SettingWindow/WinHair/H_Hair/SelectMenu");
                hairSettingsGroup = GameObject.Find("CharaCustom/CustomControl/CanvasSub/SettingWindow/WinHair/H_Hair/Setting");
                (hairTab, hairTabContent) = RG_MaterialModUI.CreateMakerTab(hairSelectMenu, hairSettingsGroup);

                hairTab.onValueChanged.AddListener((UnityAction<bool>)Make);
                void Make(bool isOn)
                {
                    kindIndex = cvsH_Hair.SNo;
                    if (isOn) MakeHairDropdown(characterContent, kindIndex);
                }

                // Make when entering hair section
                Toggle hairMainToggle = GameObject.Find("CharaCustom/CustomControl/CanvasMain/MainMenu/tglHair").GetComponent<Toggle>();
                GameObject settingWindow = GameObject.Find("CharaCustom/CustomControl/CanvasSub/SettingWindow");
                hairMainToggle.onValueChanged.AddListener((UnityAction<bool>)valueChanged);
                void valueChanged(bool isOn)
                {
                    Debug.Log("HairDeselect: " + isOn);
                    if (isOn)
                    {
                        if (hairSelectMenu.GetComponentsInChildren<UI_ToggleEx>(false).Count > 5)
                             RG_MaterialModUI.ChangeWindowSize(502f, settingWindow);
                        else RG_MaterialModUI.ChangeWindowSize(428, settingWindow);
                        MakeHairDropdown(characterContent, kindIndex);
                    }
                }
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(CvsH_Hair), nameof(CvsH_Hair.SetDrawSettingByHair))]
            private static void SetDrawSettingByHair(CvsH_Hair __instance)
            {
                Debug.Log("= CvsH_Hair.SetDrawSettingByHair");
                GameObject characterObject = __instance.chaCtrl.gameObject;
                string characterName = characterObject.name;
                CharacterContent characterContent = CharactersLoaded[characterName];
                ChaControl chaControl = characterContent.chaControl;
                characterContent.enableSetTextures = true;
                int kindIndex = __instance.SNo;

                MakeHairDropdown(characterContent, kindIndex);

                // Change setting window size
                GameObject settingWindow = GameObject.Find("CharaCustom/CustomControl/CanvasSub/SettingWindow");
                Toggle hairMainToggle = GameObject.Find("CharaCustom/CustomControl/CanvasMain/MainMenu/tglHair").GetComponent<Toggle>();
                if (hairMainToggle.isOn && hairSelectMenu.GetComponentsInChildren< UI_ToggleEx>(false).Count > 5)
                {
                    RG_MaterialModUI.ChangeWindowSize(502f, settingWindow);
                }
                else
                {
                    RG_MaterialModUI.ChangeWindowSize(428f, settingWindow);
                }

                //SetAllTextures(characterName);
                for (int i = 0; i < 4; i++) SetKind(characterName, TextureDictionaries.hairTextures, i);
            }

            [HarmonyPrefix]
            [HarmonyPatch(typeof(ChaControl), nameof(ChaControl.ChangeHair), typeof(int), typeof(int), typeof(bool))]
            private static void ChangeHair1(ChaControl __instance, int kind, int id)
            {
                Debug.Log("ChangeHair1: kind " + kind + " id " + id);
                string characterName = __instance.gameObject.name;
                ResetKind(characterName, TextureDictionaries.hairTextures, kind);
            }

            //[HarmonyPrefix]
            //[HarmonyPatch(typeof(ChaControl), nameof(ChaControl.ChangeSettingHairColor))]
            //private static void ChangeSettingHairColor(ChaControl __instance, int parts)
            //{
            //    Debug.Log("ChangeSettingHairColor: " + parts);
            //}

            //[HarmonyPostfix]
            //[HarmonyPatch(typeof(ChaControl), nameof(ChaControl.ChangeHairAll))]
            //private static void ChangeHairAll()
            //{
            //    Debug.Log("ChangeHairAll");
            //}
        }
    }
}

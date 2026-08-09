using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace F1AR.Editor
{
    /// <summary>
    /// Utility menu to apply and re-bind all F1 Car textures directly to scene MeshRenderers.
    /// </summary>
    public static class ApplyF1MaterialsToScene
    {
        private const string TexturesFolder = "Assets/Models/AstonMartin/textures";

        [MenuItem("Tools/F1 AR/Auto-Assign Textures To Selected Model")]
        public static void AssignTexturesToSelected()
        {
            GameObject selectedObj = Selection.activeGameObject;
            if (selectedObj == null)
            {
                Debug.LogWarning("[ApplyF1MaterialsToScene] Please select the F1 Car GameObject in your Hierarchy first!");
                return;
            }

            // Ensure materials are set up first
            AutoSetupF1Materials.SetupMaterials();

            Renderer[] renderers = selectedObj.GetComponentsInChildren<Renderer>(true);
            int updatedMaterials = 0;

            Shader urpShader = Shader.Find("Universal Render Pipeline/Lit");

            foreach (Renderer renderer in renderers)
            {
                Material[] mats = renderer.sharedMaterials;
                bool modified = false;

                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;

                    string matName = mats[i].name.Replace(" (Instance)", "");
                    
                    // Match material name to textures
                    Texture2D baseMap = FindTextureForMaterial(matName, "BaseCol");
                    Texture2D normalMap = FindTextureForMaterial(matName, "Normal");
                    Texture2D metallicMap = FindTextureForMaterial(matName, "Metal");

                    if (baseMap != null || normalMap != null)
                    {
                        Material matToUpdate = mats[i];

                        // Ensure shader is URP Lit
                        if (urpShader != null && matToUpdate.shader != urpShader)
                        {
                            matToUpdate.shader = urpShader;
                        }

                        Undo.RecordObject(matToUpdate, "Update F1 Material");

                        if (baseMap != null)
                        {
                            matToUpdate.SetTexture("_BaseMap", baseMap);
                            matToUpdate.SetTexture("_MainTex", baseMap);
                        }

                        if (normalMap != null)
                        {
                            matToUpdate.SetTexture("_BumpMap", normalMap);
                            matToUpdate.EnableKeyword("_NORMALMAP");
                        }

                        if (metallicMap != null)
                        {
                            matToUpdate.SetTexture("_MetallicGlossMap", metallicMap);
                            matToUpdate.EnableKeyword("_METALLICSPECGLOSSMAP");
                        }

                        EditorUtility.SetDirty(matToUpdate);
                        modified = true;
                        updatedMaterials++;
                    }
                }

                if (modified)
                {
                    renderer.sharedMaterials = mats;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[ApplyF1MaterialsToScene] Successfully updated {updatedMaterials} material slots on '{selectedObj.name}' with URP PBR textures!");
        }

        private static Texture2D FindTextureForMaterial(string matName, string mapType)
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { TexturesFolder });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string filename = Path.GetFileNameWithoutExtension(path);

                if (filename.Contains(mapType, StringComparison.OrdinalIgnoreCase))
                {
                    // Check if material name matches filename segment
                    if (filename.Contains(matName, StringComparison.OrdinalIgnoreCase) ||
                        matName.Contains(ExtractKey(filename), StringComparison.OrdinalIgnoreCase))
                    {
                        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    }
                }
            }

            // Fallback fuzzy search
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string filename = Path.GetFileNameWithoutExtension(path);

                if (filename.Contains(mapType, StringComparison.OrdinalIgnoreCase))
                {
                    // Check key sub-parts (e.g. chassis, wheels, jant, wing, spoiler, etc.)
                    string key = ExtractKey(filename);
                    if (!string.IsNullOrEmpty(key) && matName.Contains(key, StringComparison.OrdinalIgnoreCase))
                    {
                        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    }
                }
            }

            return null;
        }

        private static string ExtractKey(string filename)
        {
            string clean = filename.Replace("formula2v4nomaterila_", "");
            int underscoreIndex = clean.IndexOf('_');
            if (underscoreIndex > 0)
            {
                return clean.Substring(0, underscoreIndex);
            }
            return clean;
        }
    }
}

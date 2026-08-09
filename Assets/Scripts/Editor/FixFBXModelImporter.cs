using System.IO;
using UnityEditor;
using UnityEngine;

namespace F1AR.Editor
{
    /// <summary>
    /// Editor utility to fix self-intersecting polygon warnings on FBX models by enabling vertex welding and mesh optimization.
    /// </summary>
    public static class FixFBXModelImporter
    {
        private const string FbxPath = "Assets/Models/Pod/anakins-pod-star-wars/source/pod_complet_low.fbx";

        [MenuItem("Tools/F1 AR/Fix FBX Model Importer Warnings")]
        public static void FixFbxImporterSettings()
        {
            ModelImporter importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;

            if (importer == null)
            {
                // Find any FBX model in Assets/Models if specific path moved
                string[] guids = AssetDatabase.FindAssets("t:Model", new[] { "Assets/Models" });
                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
                    {
                        ModelImporter mi = AssetImporter.GetAtPath(path) as ModelImporter;
                        if (mi != null)
                        {
                            OptimizeImporter(mi, path);
                        }
                    }
                }
            }
            else
            {
                OptimizeImporter(importer, FbxPath);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void OptimizeImporter(ModelImporter importer, string path)
        {
            importer.weldVertices = true;
            importer.importBlendShapes = false;
            importer.addCollider = false; // We use convex MeshColliders / BoxColliders on demand
            importer.optimizeMeshPolygons = true;
            importer.optimizeMeshVertices = true;

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            Debug.Log($"🔧 [FixFBXModelImporter] Optimized FBX Model Importer settings for '{path}' (Vertices Welded, Mesh Polygons Optimized).");
        }
    }
}

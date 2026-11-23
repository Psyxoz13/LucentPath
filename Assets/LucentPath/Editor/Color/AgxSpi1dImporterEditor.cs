using LucentPath.Rendering;
using System.IO;
using UnityEditor;
using UnityEditor.AssetImporters;

namespace LucentPath.Editor.Color
{
    [CustomEditor(typeof(AgxSpi1dImporter))]
    public class AgxSpi1dImporterEditor : ScriptedImporterEditor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            string path = ((AssetImporter)target).assetPath;
            var lut = AssetDatabase.LoadAssetAtPath<AgxSpi1dLut>(path);

            if (lut != null)
            {
                EditorGUILayout.LabelField("Source URL", lut.SourceUrl);
                EditorGUILayout.LabelField("Blob SHA", lut.SourceBlobSha);
                EditorGUILayout.LabelField("File Size", new FileInfo(path).Length + " bytes");
                EditorGUILayout.LabelField("Version", lut.Version.ToString());
                EditorGUILayout.LabelField("From", $"{lut.From} .. {lut.To}");
                EditorGUILayout.LabelField("Length", lut.Length.ToString());
                EditorGUILayout.LabelField("Components", lut.Components.ToString());
                EditorGUILayout.LabelField("Values", lut.Values != null ? lut.Values.Length.ToString() : "0");

                MessageType messageType = lut.IsValidOfficialDefaultContrast ? MessageType.Info : MessageType.Error;

                EditorGUILayout.HelpBox(lut.ValidationMessage, messageType);
            }

            serializedObject.ApplyModifiedProperties();
            ApplyRevertGUI();
        }
    }
}

using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(LLMController))]
public class TerrainLLMEditor : Editor
{
    private bool isProcessing = false;
    private SerializedProperty apiKeyProperty;
    private SerializedProperty inputImageProperty;

    private void OnEnable()
    {
        apiKeyProperty = serializedObject.FindProperty("apiKey");
        inputImageProperty = serializedObject.FindProperty("inputImage");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        LLMController controller = (LLMController)target;

        EditorGUILayout.PropertyField(apiKeyProperty);

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Procedural Terrain Generation from Image", EditorStyles.boldLabel);
        
        EditorGUILayout.PropertyField(inputImageProperty, new GUIContent("Input Image"));
        Texture2D inputImage = inputImageProperty?.objectReferenceValue as Texture2D;

        // Draw preview if we have an image
        if (inputImage != null)
        {
            EditorGUILayout.Space(5);
            // Calculate preview
            float aspect = (float)inputImage.width / inputImage.height;
            float previewWidth = EditorGUIUtility.currentViewWidth - 40;
            float previewHeight = Mathf.Min(200f, previewWidth / aspect);
            Rect previewRect = GUILayoutUtility.GetRect(previewWidth, previewHeight);
            previewRect.width = previewHeight * aspect;
            previewRect.x = EditorGUIUtility.currentViewWidth / 2 - previewRect.width / 2;
            // Draw
            EditorGUI.DrawPreviewTexture(previewRect, inputImage);
        }

        EditorGUILayout.Space(10);

        // Generate button
        GUI.enabled = inputImage != null && !isProcessing;
        if (GUILayout.Button("Generate Terrain", GUILayout.Height(30)))
        {
            GenerateTerrain(controller, inputImage);
        }
        
        // Reapply button
        GUI.enabled = !isProcessing && controller.HasLastCommands;
        if (GUILayout.Button("Reapply", GUILayout.Height(30)))
        {
            EditorUtility.DisplayProgressBar("Reapplying Terrain", "Executing previous commands...", 0.5f);
            try
            {
                controller.Reapply();
                EditorUtility.SetDirty(controller.gameObject.GetComponent<Terrain>());
                SceneView.RepaintAll();
            }
            catch (System.Exception e)
            {
                EditorUtility.DisplayDialog("Error", $"Failed to reapply terrain: {e.Message}", "OK");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                Repaint();
            }
        }
        GUI.enabled = true;

        if (serializedObject.hasModifiedProperties)
        {
            serializedObject.ApplyModifiedProperties();
        }
    }

    private async void GenerateTerrain(LLMController controller, Texture2D image)
    {
        isProcessing = true;
        EditorUtility.DisplayProgressBar("Generating Terrain", "Processing image...", 0.5f);
        
        try
        {        
            string commands = await controller.GenerateTerrainPromptFromImage(image);
            if (!string.IsNullOrEmpty(commands))
            {   
                EditorUtility.DisplayProgressBar("Generating Terrain", "Executing commands...", 0.8f);
                
                EditorApplication.delayCall += () =>
                {
                    controller.ExecuteTerrainCommands(commands);
                    EditorUtility.SetDirty(controller.gameObject.GetComponent<Terrain>());
                    SceneView.RepaintAll();
                };
            }
            else
            {
                EditorUtility.DisplayDialog("Error", "Failed to generate terrain commands from image", "OK");
            }
        }
        catch (System.Exception e)
        {
            EditorUtility.DisplayDialog("Error", $"Failed to generate terrain: {e.Message}", "OK");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            isProcessing = false;
            Repaint();
        }
    }
} 
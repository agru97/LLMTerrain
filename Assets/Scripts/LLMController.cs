using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Threading.Tasks;
using System.Text;
using System.IO;
using System.Linq;
using UnityEditor;
using System.Collections.Generic;

[ExecuteInEditMode]
public class LLMController : MonoBehaviour
{
    private const string GEMINI_API_ENDPOINT = "https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-pro:generateContent";
    
    [SerializeField] private string apiKey;
    [SerializeField] private Texture2D inputImage;
    [SerializeField] private string lastCommands;
    private CustomTerrain terrainGenerator;
    
    public bool HasLastCommands => !string.IsNullOrEmpty(lastCommands);

    private void Awake()
    {
        terrainGenerator = GetComponent<CustomTerrain>();
        if (terrainGenerator == null)
        {
            Debug.LogError("No CustomTerrain component found!");
        }
    }

    public async Task<string> GenerateTerrainPromptFromImage(Texture2D image)
    {
        try
        {
            string base64Image = ConvertTextureToBase64(image);

            string jsonPrompt = CreateJsonPrompt(base64Image);
            
            string response = await SendGeminiRequest(jsonPrompt);
            
            Debug.Log($"<color=yellow>Raw Gemini Response:</color>\n{response}");
            
            return ParseGeminiResponse(response);
        }
        catch (Exception e)
        {
            Debug.LogError($"Error generating terrain prompt: {e.Message}");
            return null;
        }
    }

    // Convert Texture2D to base64
    private string ConvertTextureToBase64(Texture2D sourceTexture)
    {
        try
        {
            RenderTexture rt = RenderTexture.GetTemporary(
                sourceTexture.width,
                sourceTexture.height,
                0,
                RenderTextureFormat.Default,
                RenderTextureReadWrite.Linear
            );

            Graphics.Blit(sourceTexture, rt);

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;

            Texture2D readableTexture = new Texture2D(
                sourceTexture.width,
                sourceTexture.height,
                TextureFormat.RGB24,
                false
            );

            readableTexture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            readableTexture.Apply();

            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);

            byte[] imageBytes = readableTexture.EncodeToJPG();
            
            DestroyImmediate(readableTexture);
            
            return Convert.ToBase64String(imageBytes);
        }
        catch (Exception e)
        {
            Debug.LogError($"Error converting texture to base64: {e.Message}");
            return null;
        }
    }

    private string CreateJsonPrompt(string base64Image)
    {
        string prompt = @"Analyze this landscape image and provide Unity terrain generation commands.
                        If NO water is visible in the image, do not include any water creating commands.
                        If water IS visible, estimate its relative height compared to the terrain (knowing terrain height ranges from 0 to 1).

                        Return ONLY a chain of method calls in this exact format:
                        method1(param1=value1,param2=value2);method2(param1=value1,param2=value2);...

                        IMPORTANT: 
                        1. Voronoi and MidPointDisplacement must be used FIRST if you want to use them, as they create the base terrain!
                        2. Water height must be carefully set relative to terrain features - if water exists, it should match the image.
                           IMPORTANT: It's better to set water height too low than too high! (0.0-0.2 range)

                        Available methods and their parameter ranges:

                        Base Terrain Generators (If you want to use them, use these first):
                        
                        Voronoi(fallOff=0.0-10.0, dropOff=0.0-10.0, minHeight=0-1.0, maxHeight=0.0-1.0, peaks=1-10)
                        - Creates normal mountain peaks
                        - Higher fallOff = steeper peaks

                        MidPointDisplacement(heightMin=-0-1, heightMax=0-1, smoothness=0.5-1.0)
                        - Creates natural-looking varied terrain, or sharp rocky jagged terrain
                        - lower smoothness = sharp jagged terrain

                        Terrain Modifiers and Erosion:

                        ThermalErosion(erosionStrength=0.1-1.0, erosionAmount=0.01-0.1, iterations=1-20)
                        - Simulates rock crumbling and settling
                        - Higher strength = more aggressive erosion
                        - More iterations = more settled terrain

                        WindErosion(erosionStrength=0.1-1.0, iterations=1-10)
                        - Simulates wind-based erosion
                        - Creates dunes and wind-swept features
                        - More iterations = stronger wind effects

                        CanyonErosion(digDepth=0.01-0.1, bankSlope=0.001-0.01, iterations=1-5)
                        - Creates river canyons and valleys
                        - Higher digDepth = deeper canyons
                        - More iterations = deeper and more complex canyons

                        Smooth(smoothAmount=1-10)
                        - Softens terrain features
                        - Higher amount = smoother result

                        AddWater(height=0.0-1.0)
                        - ONLY use if water is clearly visible in the image
                        - Height must be relative to terrain features (0-1 range)

                        AddSplatMaps(texture1Height=0-1.0,texture1Color=#RRGGBB,texture1Offset=0.05-0.2,texture2Height=0-1.0,texture2Color=#RRGGBB,texture2Offset=0.05-0.2,texture3Height=0-1.0,texture3Color=#RRGGBB,texture3Offset=0.05-0.2)
                        - Applies 1-3 color(s) to different height ranges of the terrain
                        - Specify 1-3 height ranges, colors (hex format), and blend offsets
                        - Do not mistake the water in the image for a texture
                        - Heights must be in ascending order
                        - Offset controls how much textures blend (higher = more blending)
                        ";

        return $@"{{
            ""contents"": [{{
                ""parts"":[
                    {{""text"": ""{prompt}""}},
                    {{
                        ""inline_data"": {{
                            ""mime_type"":""image/jpeg"",
                            ""data"": ""{base64Image}""
                        }}
                    }}
                ]
            }}]
        }}";
    }

    private async Task<string> SendGeminiRequest(string jsonPrompt)
    {
        using (UnityWebRequest request = new UnityWebRequest($"{GEMINI_API_ENDPOINT}?key={apiKey}", "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPrompt);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            await request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                throw new Exception($"API request failed: {request.error}");
            }

            return request.downloadHandler.text;
        }
    }

    private string ParseGeminiResponse(string response)
    {
        try
        {     
            var jsonResponse = JsonUtility.FromJson<JSONResponse>(response);
            string commands = jsonResponse.candidates[0].content.parts[0].text;
            
            if (string.IsNullOrEmpty(commands))
            {
                Debug.LogError("Empty command sequence received");
                return null;
            }

            commands = commands.Trim();
            
            if (!commands.Contains("(") || !commands.Contains(")"))
            {
                Debug.LogError("Invalid command format received");
                Debug.Log($"Received text: {commands}");
                return null;
            }

            Debug.Log($"<color=yellow>Parsed commands:</color> {commands}");
            return commands;
        }
        catch (Exception e)
        {
            Debug.LogError($"Error parsing response: {e.Message}\nRaw Response: {response}");
            return null;
        }
    }

    public void ExecuteTerrainCommands(string commands)
    {
        lastCommands = commands;
        if (terrainGenerator == null)
        {
            terrainGenerator = GetComponent<CustomTerrain>();
            if (terrainGenerator == null)
            {
                Debug.LogError("Couldn't find CustomTerrain component!");
                return;
            }
        }

        CleanUpTerrain();
        
        // Split commands into method calls
        string[] methodCalls = commands.Split(';')
            .Where(cmd => !string.IsNullOrWhiteSpace(cmd))
            .ToArray();

        foreach (string methodCall in methodCalls)
        {
            string trimmedCall = methodCall.Trim();
            int openParenIndex = trimmedCall.IndexOf('(');
            int closeParenIndex = trimmedCall.LastIndexOf(')');
            
            if (openParenIndex == -1 || closeParenIndex == -1)
            {
                Debug.LogError($"Invalid method call format: {trimmedCall}");
                continue;
            }

            string methodName = trimmedCall.Substring(0, openParenIndex).Trim();
            string parameters = trimmedCall.Substring(openParenIndex + 1, 
                closeParenIndex - openParenIndex - 1).Trim();

            Debug.Log($"<color=yellow>Executing:</color> {methodName}: {parameters}");
            
            // Execute the method with its parameters
            ExecuteTerrainMethod(methodName, parameters);
        }

        Debug.Log($"<color=green>✓ Procedural Terrain Generation completed with commands:</color> {commands}");

        // Force terrain update
        Terrain terrain = GetComponent<Terrain>();
        Vector3 size = terrain.terrainData.size;
        terrain.terrainData.size = size;
        terrain.Flush();
    }

    private float ClampParameter(float value, float min, float max, string paramName)
    {
        if (value < min || value > max)
        {
            Debug.LogWarning($"Parameter {paramName} value {value} outside range [{min}-{max}] -> clamping");
            return Mathf.Clamp(value, min, max);
        }
        return value;
    }

    private void ExecuteTerrainMethod(string methodName, string parameters)
    {
        try
        {
            // Parse parameters
            var paramDict = parameters.Split(',')
                .Select(p => p.Trim().Split('='))
                .ToDictionary(p => p[0], p => p[1]);

            switch (methodName)
            {
                case "Voronoi":
                    terrainGenerator.voronoiFallOff = ClampParameter(float.Parse(paramDict["fallOff"]), 0f, 10f, "fallOff");
                    terrainGenerator.voronoiDropOff = ClampParameter(float.Parse(paramDict["dropOff"]), 0f, 10f, "dropOff");
                    terrainGenerator.voronoiMinHeight = ClampParameter(float.Parse(paramDict["minHeight"]), 0f, 1f, "minHeight");
                    terrainGenerator.voronoiMaxHeight = ClampParameter(float.Parse(paramDict["maxHeight"]), 0f, 1f, "maxHeight");
                    terrainGenerator.voronoiPeaks = (int)ClampParameter(float.Parse(paramDict["peaks"]), 1f, 10f, "peaks");
                    terrainGenerator.Voronoi();
                    break;

                case "MidPointDisplacement":
                    terrainGenerator.MPDHeightMin = ClampParameter(float.Parse(paramDict["heightMin"]), 0, 1f, "heightMin");
                    terrainGenerator.MPDHeightMax = ClampParameter(float.Parse(paramDict["heightMax"]), 0f, 1f, "heightMax");
                    terrainGenerator.MPDRoughness = ClampParameter(float.Parse(paramDict["smoothness"]), 0.5f, 1f, "smoothness");
                    terrainGenerator.MidPointDisplacement();
                    break;

                case "Smooth":
                    terrainGenerator.smoothAmount = (int)ClampParameter(float.Parse(paramDict["smoothAmount"]), 1f, 10f, "smoothAmount");
                    terrainGenerator.Smooth();
                    break;

                case "ThermalErosion":
                    terrainGenerator.erosionStrength = ClampParameter(float.Parse(paramDict["erosionStrength"]), 0.1f, 1.0f, "erosionStrength");
                    terrainGenerator.erosionAmount = ClampParameter(float.Parse(paramDict["erosionAmount"]), 0.01f, 0.1f, "erosionAmount");
                    terrainGenerator.erosionType = CustomTerrain.ErosionType.Thermal;
                    terrainGenerator.erosionIterations = (int)ClampParameter(float.Parse(paramDict["iterations"]), 1f, 20f, "iterations");
                    for (int i = 0; i < terrainGenerator.erosionIterations; i++)
                    {
                        terrainGenerator.Erode();
                    }
                    break;

                case "WindErosion":
                    terrainGenerator.erosionStrength = ClampParameter(float.Parse(paramDict["erosionStrength"]), 0.1f, 1.0f, "erosionStrength");
                    terrainGenerator.erosionType = CustomTerrain.ErosionType.Wind;
                    terrainGenerator.erosionIterations = (int)ClampParameter(float.Parse(paramDict["iterations"]), 1f, 10f, "iterations");
                    for (int i = 0; i < terrainGenerator.erosionIterations; i++)
                    {
                        terrainGenerator.Erode();
                    }
                    break;

                case "CanyonErosion":
                    terrainGenerator.erosionStrength = ClampParameter(float.Parse(paramDict["digDepth"]), 0.01f, 0.1f, "digDepth");
                    terrainGenerator.erosionAmount = ClampParameter(float.Parse(paramDict["bankSlope"]), 0.001f, 0.01f, "bankSlope");
                    terrainGenerator.erosionType = CustomTerrain.ErosionType.Canyon;
                    terrainGenerator.erosionIterations = (int)ClampParameter(float.Parse(paramDict["iterations"]), 1f, 5f, "iterations");
                    for (int i = 0; i < terrainGenerator.erosionIterations; i++)
                    {
                        terrainGenerator.Erode();
                    }
                    break;

                case "AddWater":
                    terrainGenerator.waterHeight = ClampParameter(float.Parse(paramDict["height"]), 0f, 1f, "height");
                    terrainGenerator.AddWater();
                    break;

                case "AddSplatMaps":
                    int textureCount = paramDict.Count / 3; // height, color, and offset for each texture
                    terrainGenerator.splatHeights.Clear();
                    
                    // Create TerrainLayer array
                    TerrainLayer[] terrainLayers = new TerrainLayer[textureCount];
                    
                    // Sort the textures by height to ensure proper layering
                    var heightPairs = new List<(int index, float height)>();
                    for (int i = 0; i < textureCount; i++) {
                        float height = ClampParameter(float.Parse(paramDict[$"texture{i+1}Height"]), 0f, 1f, $"texture{i+1}Height");
                        heightPairs.Add((i, height));
                    }
                    heightPairs.Sort((a, b) => a.height.CompareTo(b.height));
                    
                    for (int i = 0; i < textureCount; i++) {
                        int originalIndex = heightPairs[i].index;
                        float height = heightPairs[i].height;
                        string colorHex = paramDict[$"texture{originalIndex+1}Color"].TrimStart('#');
                        float offset = ClampParameter(float.Parse(paramDict[$"texture{originalIndex+1}Offset"]), 0.05f, 0.2f, $"texture{originalIndex+1}Offset");
                        
                        // Convert hex to RGB
                        Color color = new Color(
                            Convert.ToInt32(colorHex.Substring(0, 2), 16) / 255f,
                            Convert.ToInt32(colorHex.Substring(2, 2), 16) / 255f,
                            Convert.ToInt32(colorHex.Substring(4, 2), 16) / 255f
                        );
                        
                        // Create texture with more resolution
                        Texture2D tex = new Texture2D(512, 512, TextureFormat.RGB24, false);
                        Color[] colors = new Color[512 * 512];
                        for (int p = 0; p < colors.Length; p++) {
                            colors[p] = color;
                        }
                        tex.SetPixels(colors);
                        tex.Apply();
                        
                        // Create and save TerrainLayer
                        TerrainLayer terrainLayer = new TerrainLayer();
                        terrainLayer.diffuseTexture = tex;
                        terrainLayer.tileSize = new Vector2(50, 50);
                        terrainLayer.tileOffset = Vector2.zero;
                        terrainLayer.specular = Color.white;
                        terrainLayer.metallic = 0f;
                        terrainLayer.smoothness = 0f;
                        
                        // Save assets
                        string texPath = $"Assets/TerrainTexture_{i}.asset";
                        AssetDatabase.CreateAsset(tex, texPath);
                        
                        string layerPath = $"Assets/TerrainLayer_{i}.terrainlayer";
                        AssetDatabase.CreateAsset(terrainLayer, layerPath);
                        AssetDatabase.SaveAssets();
                        
                        terrainLayers[i] = terrainLayer;
                        
                        // Add to splatHeights with proper height ranges
                        terrainGenerator.splatHeights.Add(new CustomTerrain.SplatHeights {
                            texture = tex,
                            minHeight = height,
                            maxHeight = (i < textureCount - 1) ? heightPairs[i + 1].height : 1.0f,
                            splatOffset = offset,
                            tileSize = new Vector2(50, 50),
                            minSlope = 0,
                            maxSlope = 90 // Allow texture on any slope
                        });
                    }
                    
                    // Set the TerrainLayers through the Terrain component
                    Terrain terrain = terrainGenerator.GetComponent<Terrain>();
                    terrain.terrainData.terrainLayers = terrainLayers;
                    
                    // Force terrain to update its material
                    terrain.materialTemplate = new Material(Shader.Find("Universal Render Pipeline/Terrain/Lit"));
                    
                    // Use existing SplatMaps method
                    terrainGenerator.SplatMaps();
                    
                    // Force terrain update
                    terrain.Flush();
                    break;

                default:
                    Debug.LogWarning($"Unknown method: {methodName}");
                    break;
            }

            Debug.Log($"<color=green>Successfully executed {methodName}</color>");
        }
        catch (Exception e)
        {
            Debug.LogError($"Error executing method {methodName}: {e.Message}\nStackTrace: {e.StackTrace}");
        }
    }

    public void Reapply()
    {

        if (!string.IsNullOrEmpty(lastCommands))
        {
            CleanUpTerrain();
            ExecuteTerrainCommands(lastCommands);
        }
        else
        {
            Debug.LogWarning("No previous commands to reapply");
        }
    }

    public void CleanUpTerrain()
    {
        // Remove water
        GameObject existingWater = GameObject.Find("water");
        if (existingWater != null)
        {
            DestroyImmediate(existingWater);
        }

        // Clear splat maps and terrain layers
        Terrain terrain = terrainGenerator.GetComponent<Terrain>();
        if (terrain != null && terrain.terrainData != null)
        {
            terrainGenerator.splatHeights.Clear();
            // Add at least one empty splat height to prevent null reference
            terrainGenerator.splatHeights.Add(new CustomTerrain.SplatHeights());
            terrain.terrainData.terrainLayers = new TerrainLayer[0];
        }

        // Reset terrain heights
        terrainGenerator.ResetTerrain();
    }
} 
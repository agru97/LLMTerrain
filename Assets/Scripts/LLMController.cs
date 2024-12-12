using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Threading.Tasks;
using System.Text;
using System.IO;
using System.Linq;

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
                        - Creates mountain peaks and rocky terrain
                        - Higher fallOff = steeper peaks

                        MidPointDisplacement(heightMin=-2.0-0, heightMax=0-2.0, roughness=1.0-5.0)
                        - Creates natural-looking varied terrain
                        - Higher roughness = more jagged terrain

                        Terrain Modifiers and Erosion:

                        RainErosion(droplets=10-1000, erosionStrength=0.1-1.0, iterations=1-10)
                        - Simulates rainfall erosion
                        - More droplets = more detailed erosion
                        - Higher strength = deeper erosion channels
                        - More iterations = stronger erosion effect

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

        // Clean up
        GameObject existingWater = GameObject.Find("water");
        if (existingWater != null)
        {
            DestroyImmediate(existingWater);
        }
        terrainGenerator.ResetTerrain();
        
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
                    terrainGenerator.MPDHeightMin = ClampParameter(float.Parse(paramDict["heightMin"]), -2f, 0f, "heightMin");
                    terrainGenerator.MPDHeightMax = ClampParameter(float.Parse(paramDict["heightMax"]), 0f, 2f, "heightMax");
                    terrainGenerator.MPDRoughness = ClampParameter(float.Parse(paramDict["roughness"]), 1f, 5f, "roughness");
                    terrainGenerator.MidPointDisplacement();
                    break;

                case "Smooth":
                    terrainGenerator.smoothAmount = (int)ClampParameter(float.Parse(paramDict["smoothAmount"]), 1f, 10f, "smoothAmount");
                    terrainGenerator.Smooth();
                    break;

                case "RainErosion":
                    terrainGenerator.droplets = (int)ClampParameter(float.Parse(paramDict["droplets"]), 10f, 1000f, "droplets");
                    terrainGenerator.erosionStrength = ClampParameter(float.Parse(paramDict["erosionStrength"]), 0.1f, 1.0f, "erosionStrength");
                    terrainGenerator.erosionType = CustomTerrain.ErosionType.Rain;
                    terrainGenerator.erosionIterations = (int)ClampParameter(float.Parse(paramDict["iterations"]), 1f, 10f, "iterations");
                    for (int i = 0; i < terrainGenerator.erosionIterations; i++)
                    {
                        terrainGenerator.Erode();
                    }
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
            // Clean up
            GameObject existingWater = GameObject.Find("water");
            if (existingWater != null)
            {
                DestroyImmediate(existingWater);
            }
            terrainGenerator.ResetTerrain();
            
            ExecuteTerrainCommands(lastCommands);
        }
        else
        {
            Debug.LogWarning("No previous commands to reapply");
        }
    }
} 
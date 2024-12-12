using UnityEngine;
using UnityEditor;
using EditorGUITable;

[CustomEditor(typeof(CustomTerrain))]
[CanEditMultipleObjects]

public class CustomTerrainEditor : Editor {

    // Properties
    SerializedProperty randomHeightRange;
    SerializedProperty heightMapScale;
    SerializedProperty heightMapImage;
    SerializedProperty resetTerrain;
    SerializedProperty perlinXScale;
    SerializedProperty perlinYScale;
    SerializedProperty perlinOffsetX;
    SerializedProperty perlinOffsetY;
    SerializedProperty perlinOctaves;
    SerializedProperty perlinPersistance;
    SerializedProperty perlinHeightScale;
    SerializedProperty voronoiFallOff;
    SerializedProperty voronoiDropOff;
    SerializedProperty voronoiMinHeight;
    SerializedProperty voronoiMaxHeight;
    SerializedProperty voronoiPeaks;
    SerializedProperty voronoiType;
    SerializedProperty MPDHeightMin;
    SerializedProperty MPDHeightMax;
    SerializedProperty MPDHeightDampner;
    SerializedProperty MPDRoughness;
    SerializedProperty smoothAmount;

    GUITableState splatMapTable;
    SerializedProperty splatHeights;

    GUITableState perlinParameterTable;
    SerializedProperty perlinParameters;

    GUITableState vegMapTable;
    SerializedProperty vegetation;
    SerializedProperty maxTrees;
    SerializedProperty treeSpacing;

    GUITableState detailMapTable;
    SerializedProperty details;
    SerializedProperty maxDetails;
    SerializedProperty detailSpacing;

    SerializedProperty waterHeight;
    SerializedProperty waterGO;

    SerializedProperty erosionType;
    SerializedProperty erosionStrength;
    SerializedProperty erosionAmount;
    SerializedProperty springsPerRiver;
    SerializedProperty solubilty;
    SerializedProperty droplets;
    SerializedProperty erosionSmoothAmount;

    SerializedProperty numClouds;
    SerializedProperty particlesPerCloud;
    SerializedProperty cloudScaleMin;
    SerializedProperty cloudScaleMax;
    SerializedProperty cloudMaterial;
    SerializedProperty cloudShadowMaterial;
    SerializedProperty cloudStartSize;
    SerializedProperty cloudColour;
    SerializedProperty cloudLining;
    SerializedProperty cloudMinSpeed;
    SerializedProperty cloudMaxSpeed;
    SerializedProperty cloudRange;


    // Fold Outs
    bool showRandom = false;
    bool showLoadHeights = false;
    bool showPerlinNoise = false;
    bool showMultiplePerlin = false;
    bool showVoronoi = false;
    bool showMPD = false;
    bool showSmooth = false;
    bool showSplatMaps = false;
    bool showVeg = false;
    bool showDetails = false;
    bool showWater = false;
    bool showErosion = false;
    bool showClouds = false;

    private void OnEnable() {

        randomHeightRange = serializedObject.FindProperty("randomHeightRange");
        heightMapScale = serializedObject.FindProperty("heightMapScale");
        heightMapImage = serializedObject.FindProperty("heightMapImage");
        resetTerrain = serializedObject.FindProperty("resetTerrain");
        perlinXScale = serializedObject.FindProperty("perlinXScale");
        perlinYScale = serializedObject.FindProperty("perlinYScale");
        perlinOffsetX = serializedObject.FindProperty("perlinOffsetX");
        perlinOffsetY = serializedObject.FindProperty("perlinOffsetY");
        perlinOctaves = serializedObject.FindProperty("perlinOctaves");
        perlinPersistance = serializedObject.FindProperty("perlinPersistance");
        perlinHeightScale = serializedObject.FindProperty("perlinHeightScale");
        voronoiFallOff = serializedObject.FindProperty("voronoiFallOff");
        voronoiDropOff = serializedObject.FindProperty("voronoiDropOff");
        voronoiMinHeight = serializedObject.FindProperty("voronoiMinHeight");
        voronoiMaxHeight = serializedObject.FindProperty("voronoiMaxHeight");
        voronoiPeaks = serializedObject.FindProperty("voronoiPeaks");
        voronoiType = serializedObject.FindProperty("voronoiType");
        MPDHeightMin = serializedObject.FindProperty("MPDHeightMin");
        MPDHeightMax = serializedObject.FindProperty("MPDHeightMax");
        MPDHeightDampner = serializedObject.FindProperty("MPDHeightDampner");
        MPDRoughness = serializedObject.FindProperty("MPDRoughness");
        smoothAmount = serializedObject.FindProperty("smoothAmount");
        splatMapTable = new GUITableState("splatMapTable");
        splatHeights = serializedObject.FindProperty("splatHeights");
        vegMapTable = new GUITableState("vegMapTable");
        vegetation = serializedObject.FindProperty("vegetation");
        maxTrees = serializedObject.FindProperty("maxTrees");
        treeSpacing = serializedObject.FindProperty("treeSpacing");

        perlinParameterTable = new GUITableState("perlinParameterTable");
        perlinParameters = serializedObject.FindProperty("perlinParameters");

        //hmTexture = new Texture2D(513, 513, TextureFormat.ARGB32, false);

        detailMapTable = new GUITableState("detailMapTable");
        details = serializedObject.FindProperty("details");
        maxDetails = serializedObject.FindProperty("maxDetails");
        detailSpacing = serializedObject.FindProperty("detailSpacing");

        waterHeight = serializedObject.FindProperty("waterHeight");
        waterGO = serializedObject.FindProperty("waterGO");

        erosionType = serializedObject.FindProperty("erosionType");
        erosionStrength = serializedObject.FindProperty("erosionStrength");
        erosionAmount = serializedObject.FindProperty("erosionAmount");
        springsPerRiver = serializedObject.FindProperty("springsPerRiver");
        solubilty = serializedObject.FindProperty("solubilty");
        droplets = serializedObject.FindProperty("droplets");
        erosionSmoothAmount = serializedObject.FindProperty("erosionSmoothAmount");

        numClouds = serializedObject.FindProperty("numClouds");
        particlesPerCloud = serializedObject.FindProperty("particlesPerCloud");
        cloudScaleMin = serializedObject.FindProperty("cloudScaleMin");
        cloudScaleMax = serializedObject.FindProperty("cloudScaleMax");
        cloudMaterial = serializedObject.FindProperty("cloudMaterial");
        cloudStartSize = serializedObject.FindProperty("cloudStartSize");
        cloudColour = serializedObject.FindProperty("cloudColour");
        cloudLining = serializedObject.FindProperty("cloudLining");
        cloudMinSpeed = serializedObject.FindProperty("cloudMinSpeed");
        cloudMaxSpeed = serializedObject.FindProperty("cloudMaxSpeed");
        cloudRange = serializedObject.FindProperty("cloudRange");
        cloudShadowMaterial = serializedObject.FindProperty("cloudShadowMaterial");
    }

    Vector2 scrollPos;

    public override void OnInspectorGUI() {

        serializedObject.Update();

        CustomTerrain terrain = (CustomTerrain)target;

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
        EditorGUI.indentLevel++;

        EditorGUILayout.PropertyField(resetTerrain);

        showRandom = EditorGUILayout.Foldout(showRandom, "Random");
        if (showRandom) {

            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            GUILayout.Label("Set Heights Between Random Values", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(randomHeightRange);
            if (GUILayout.Button("Random Heights")) {

                terrain.RandomTerrain();
            }
        }

        showLoadHeights = EditorGUILayout.Foldout(showLoadHeights, "Load Heights");
        if (showLoadHeights) {

            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            GUILayout.Label("Load Heights From Texture", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(heightMapImage);
            EditorGUILayout.PropertyField(heightMapScale);
            if (GUILayout.Button("Load Texture")) {

                terrain.LoadTexture();
            }
        }


        showPerlinNoise = EditorGUILayout.Foldout(showPerlinNoise, "Single Perlin Noise");
        if (showPerlinNoise) {

            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            GUILayout.Label("Perlin Noise", EditorStyles.boldLabel);
            EditorGUILayout.Slider(perlinXScale, 0.0f, 0.1f, new GUIContent("X Scale"));
            EditorGUILayout.Slider(perlinYScale, 0.0f, 0.1f, new GUIContent("Y Scale"));
            EditorGUILayout.IntSlider(perlinOffsetX, 0, 10000, new GUIContent("X Offset"));
            EditorGUILayout.IntSlider(perlinOffsetY, 0, 10000, new GUIContent("Y Offset"));
            EditorGUILayout.IntSlider(perlinOctaves, 1, 10, new GUIContent("Octaves"));
            EditorGUILayout.Slider(perlinPersistance, 0.1f, 10.1f, new GUIContent("Persistance"));
            EditorGUILayout.Slider(perlinHeightScale, 0.0f, 1.0f, new GUIContent("Height Scale"));

            if (GUILayout.Button("Perlin")) {

                terrain.Perlin();
            }
        }

        showMultiplePerlin = EditorGUILayout.Foldout(showMultiplePerlin, "Multiple Perlin Noise");
        if (showMultiplePerlin) {

            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            GUILayout.Label("Mulitple Perlin Noise", EditorStyles.boldLabel);
            perlinParameterTable = GUITableLayout.DrawTable(perlinParameterTable,
                                        serializedObject.FindProperty("perlinParameters"));
            GUILayout.Space(20);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("+")) {

                terrain.AddNewPerlin();
            }
            if (GUILayout.Button("-")) {

                terrain.RemovePerlin();
            }
            EditorGUILayout.EndHorizontal();
            if (GUILayout.Button("Apply Multiple Perlin")) {

                terrain.MultiplePerlinTerrain();
            }
            GUILayout.Space(20);
            if (GUILayout.Button("Apply Ridge Noise")) {

                terrain.RidgeNoise();
            }
        }

        showVoronoi = EditorGUILayout.Foldout(showVoronoi, "Voronoi");
        if (showVoronoi) {

            EditorGUILayout.IntSlider(voronoiPeaks, 1, 10, new GUIContent("Peak Count"));
            EditorGUILayout.Slider(voronoiFallOff, 0.0f, 10.0f, new GUIContent("Fall Off"));
            EditorGUILayout.Slider(voronoiDropOff, 0.0f, 10.0f, new GUIContent("Drop Off"));
            EditorGUILayout.Slider(voronoiMinHeight, 0.0f, 1.0f, new GUIContent("Min Height"));
            EditorGUILayout.Slider(voronoiMaxHeight, 0.0f, 1.0f, new GUIContent("Max Height"));
            EditorGUILayout.PropertyField(voronoiType);

            if (GUILayout.Button("Veronoi")) {

                terrain.Voronoi();
            }
        }

        showMPD = EditorGUILayout.Foldout(showMPD, "Mid Point Displacement");
        if (showMPD) {

            EditorGUILayout.PropertyField(MPDHeightMin);
            EditorGUILayout.PropertyField(MPDHeightMax);
            EditorGUILayout.PropertyField(MPDHeightDampner);
            EditorGUILayout.PropertyField(MPDRoughness);

            if (GUILayout.Button("MPD")) {

                terrain.MidPointDisplacement();
            }
        }



        showSplatMaps = EditorGUILayout.Foldout(showSplatMaps, "Splat Maps");
        if (showSplatMaps) {

            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            GUILayout.Label("Splat Maps", EditorStyles.boldLabel);

            splatMapTable = GUITableLayout.DrawTable(splatMapTable, serializedObject.FindProperty("splatHeights"));

            GUILayout.Space(20);
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("+")) {

                terrain.AddNewSplatHeight();
            }

            if (GUILayout.Button("-")) {

                terrain.RemoveSplatHeights();
            }

            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("Apply SplatMaps")) {

                terrain.SplatMaps();
            }
        }


        showVeg = EditorGUILayout.Foldout(showVeg, "Vegetaion");
        if (showVeg) {

            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            GUILayout.Label("Vegetaion", EditorStyles.boldLabel);
            EditorGUILayout.IntSlider(maxTrees, 0, 10000, new GUIContent("Maximum Trees"));
            EditorGUILayout.IntSlider(treeSpacing, 2, 20, new GUIContent("Tree Spacing"));
            vegMapTable = GUITableLayout.DrawTable(vegMapTable, serializedObject.FindProperty("vegetation"));

            GUILayout.Space(20);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+")) {

                terrain.AddNewVegetation();
            }

            if (GUILayout.Button("-")) {

                terrain.RemoveVegetation();
            }

            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("Apply Vegetaion")) {

                terrain.PlantVegetation();
            }
        }

        showDetails = EditorGUILayout.Foldout(showDetails, "Details");
        if (showDetails) {

            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            GUILayout.Label("Details", EditorStyles.boldLabel);
            EditorGUILayout.IntSlider(maxDetails, 0, 10000, new GUIContent("Maximum Details"));
            EditorGUILayout.IntSlider(detailSpacing, 1, 20, new GUIContent("Detail Spacing"));
            detailMapTable = GUITableLayout.DrawTable(detailMapTable, serializedObject.FindProperty("details"));

            terrain.GetComponent<Terrain>().detailObjectDistance = maxDetails.intValue;

            GUILayout.Space(20);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+")) {

                terrain.AddNewDetails();
            }
            if (GUILayout.Button("-")) {

                terrain.RemoveDetails();
            }
            EditorGUILayout.EndHorizontal();
            if (GUILayout.Button("Apply Details")) {

                terrain.AddDetails();
            }
        }

        showWater = EditorGUILayout.Foldout(showWater, "Water");
        if (showWater) {

            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            GUILayout.Label("Water", EditorStyles.boldLabel);
            EditorGUILayout.Slider(waterHeight, 0.0f, 1.0f, new GUIContent("Water Height"));
            EditorGUILayout.PropertyField(waterGO);

            if (GUILayout.Button("Add Water")) {

                terrain.AddWater();
            }
        }

        showErosion = EditorGUILayout.Foldout(showErosion, "Erosion");
        if (showErosion) {

            EditorGUILayout.PropertyField(erosionType);
            EditorGUILayout.Slider(erosionStrength, 0.0f, 1.0f, new GUIContent("Erosion Strength"));
            EditorGUILayout.Slider(erosionAmount, 0.0f, 1.0f, new GUIContent("Erosion Amount"));
            EditorGUILayout.IntSlider(droplets, 0, 500, new GUIContent("Droplets"));
            EditorGUILayout.Slider(solubilty, 0.001f, 1.0f, new GUIContent("Solubility"));
            EditorGUILayout.IntSlider(springsPerRiver, 0, 20, new GUIContent("Springs Per River"));
            EditorGUILayout.IntSlider(erosionSmoothAmount, 0, 10, new GUIContent("Smooth Amount"));

            if (GUILayout.Button("Erode")) {

                terrain.Erode();
            }
        }

        showClouds = EditorGUILayout.Foldout(showClouds, "Clouds");
        if (showClouds) {
            EditorGUILayout.PropertyField(numClouds, new GUIContent("Number of Clouds"));
            EditorGUILayout.PropertyField(particlesPerCloud, new GUIContent("Particles Per Clouds"));
            EditorGUILayout.PropertyField(cloudStartSize, new GUIContent("Cloud Particle Size"));
            EditorGUILayout.PropertyField(cloudScaleMin, new GUIContent("Min Size"));
            EditorGUILayout.PropertyField(cloudScaleMax, new GUIContent("Max Size"));
            EditorGUILayout.PropertyField(cloudMaterial, true);
            EditorGUILayout.PropertyField(cloudShadowMaterial, true);
            EditorGUILayout.PropertyField(cloudColour, new GUIContent("Colour"));
            EditorGUILayout.PropertyField(cloudLining, new GUIContent("Lining"));
            EditorGUILayout.PropertyField(cloudMinSpeed, new GUIContent("Min Speed"));
            EditorGUILayout.PropertyField(cloudMaxSpeed, new GUIContent("Max Speed"));
            EditorGUILayout.PropertyField(cloudRange, new GUIContent("Distance Travelled"));

            if (GUILayout.Button("Generate Clouds")) {
                terrain.GenerateClouds();
            }
        }


        showSmooth = EditorGUILayout.Foldout(showSmooth, "Smooth Terrain");
        if (showSmooth) {

            EditorGUILayout.IntSlider(smoothAmount, 1, 10, new GUIContent("smoothAmount"));
            if (GUILayout.Button("Smooth")) {

                terrain.Smooth();
            }
        }

        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        if (GUILayout.Button("Reset Terrain")) {

            terrain.ResetTerrain();
        }

        EditorGUILayout.EndScrollView();

        serializedObject.ApplyModifiedProperties();
    }

}

using System;
using UnityEditor;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using UnityEngine.Rendering.Universal;

[ExecuteInEditMode]
public class CustomTerrain : MonoBehaviour {

    private Terrain terrain;
    private UnityEngine.TerrainData terrainData;

    void Awake()
    {
        terrain = GetComponent<Terrain>();
        if (terrain != null)
        {
            terrainData = terrain.terrainData;
        }
        else
        {
            Debug.LogError("No Terrain component found!");
        }
    }

    int HMR { get { return terrainData != null ? terrainData.heightmapResolution : 0; } }
    bool isRidged = false;

    public Vector2 randomHeightRange = new Vector2(0.0f, 0.1f);
    public Texture2D heightMapImage;
    public Vector3 heightMapScale = new Vector3(1.0f, 1.0f, 1.0f);
    public bool resetTerrain = true;

    public int erosionIterations = 1;

    // Erosion ***********************************************************************
    public enum ErosionType {

        Rain,
        Thermal,
        Tidal,
        River,
        Wind,
        Canyon,
        Beach
    }

    public ErosionType erosionType = ErosionType.Rain;
    public float erosionStrength = 0.1f;
    public float erosionAmount = 0.01f;
    public float solubilty = 0.01f;
    public int springsPerRiver = 5;
    public int droplets = 10;
    public int erosionSmoothAmount = 5;

    //CLOUDS -----------------------------------------------
    public int numClouds = 1;
    public int particlesPerCloud = 50;
    public Vector3 cloudScaleMin = new Vector3(1, 1, 1);
    public Vector3 cloudScaleMax = new Vector3(1, 1, 1);
    public Material cloudMaterial;
    public Material cloudShadowMaterial;
    public float cloudStartSize = 5;
    public Color cloudColour = Color.white;
    public Color cloudLining = Color.grey;
    public float cloudMinSpeed = 0.2f;
    public float cloudMaxSpeed = 0.5f;
    public float cloudRange = 500.0f;

    public void GenerateClouds() {

        GameObject cloudManager = GameObject.Find("CloudManager");
        if (!cloudManager) {

            cloudManager = new GameObject();
            cloudManager.name = "CloudManager";
            cloudManager.AddComponent<CloudManager>();
            cloudManager.transform.position = this.transform.position;
        }

        GameObject[] allClouds = GameObject.FindGameObjectsWithTag("Cloud");

        for (int i = 0; i < allClouds.Length; ++i)
            DestroyImmediate(allClouds[i]);


        for (int c = 0; c < numClouds; ++c) {

            GameObject cloudGO = new GameObject();
            cloudGO.name = "Cloud" + c;
            cloudGO.tag = "Cloud";

            cloudGO.transform.rotation = cloudManager.transform.rotation;
            cloudGO.transform.position = cloudManager.transform.position;

            CloudController cc = cloudGO.AddComponent<CloudController>();
            cc.lining = cloudLining;
            cc.colour = cloudColour;
            cc.numberOfParticles = particlesPerCloud;
            cc.minSpeed = cloudMinSpeed;
            cc.maxSpeed = cloudMaxSpeed;
            cc.distance = cloudRange;

            ParticleSystem cloudSystem = cloudGO.AddComponent<ParticleSystem>();
            Renderer cloudRend = cloudGO.GetComponent<Renderer>();
            cloudRend.material = cloudMaterial;

            cloudGO.layer = LayerMask.NameToLayer("Sky");
            GameObject cloudProjector = new GameObject();
            cloudProjector.name = "Shadow";
            cloudProjector.transform.position = cloudGO.transform.position;
            cloudProjector.transform.forward = Vector3.down;
            cloudProjector.transform.parent = cloudGO.transform;

            if (UnityEngine.Random.Range(0, 10) < 5) {

                DecalProjector cp = cloudProjector.AddComponent<DecalProjector>();
                cp.material = cloudShadowMaterial;
                cp.renderingLayerMask = (uint)LayerMask.NameToLayer("Sky");
                cp.size = new Vector3(100.0f, 100.0f, 10000.0f);
            }

            cloudRend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            cloudRend.receiveShadows = false;

            ParticleSystem.MainModule main = cloudSystem.main;
            main.loop = false;
            main.startLifetime = Mathf.Infinity;
            main.startSpeed = 0;
            main.startSize = cloudStartSize;
            main.startColor = Color.white;

            var emission = cloudSystem.emission;
            emission.rateOverTime = 0; //all at once
            emission.SetBursts(new ParticleSystem.Burst[] {
                    new ParticleSystem.Burst(0.0f, (short)particlesPerCloud) });

            var shape = cloudSystem.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            Vector3 newScale = new Vector3(UnityEngine.Random.Range(cloudScaleMin.x, cloudScaleMax.x),
                                           UnityEngine.Random.Range(cloudScaleMin.y, cloudScaleMax.y),
                                           UnityEngine.Random.Range(cloudScaleMin.z, cloudScaleMax.z));
            shape.scale = newScale;

            cloudGO.transform.parent = cloudManager.transform;
            cloudGO.transform.localScale = new Vector3(1.0f, 1.0f, 1.0f);
        }
    }

    public void Erode() {

        switch (erosionType) {
            case ErosionType.Rain:
                Rain();
                break;
            case ErosionType.Thermal:
                Thermal();
                break;
            case ErosionType.Tidal:
                Tidal();
                break;
            case ErosionType.River:
                River();
                break;
            case ErosionType.Wind:
                Wind();
                break;
            case ErosionType.Canyon:
                DigCanyon();
                break;
        }

        smoothAmount = erosionSmoothAmount;
        Smooth();
    }

    private void Rain() {

        float[,] heightMap = terrainData.GetHeights(0, 0, HMR, HMR);

        for (int i = 0; i < droplets; ++i) {

            heightMap[UnityEngine.Random.Range(0, HMR), UnityEngine.Random.Range(0, HMR)] -= erosionStrength;
        }
        terrainData.SetHeights(0, 0, heightMap);
    }

    private void Thermal() {

        float[,] heightMap = terrainData.GetHeights(0, 0, HMR, HMR);

        for (int y = 0; y < HMR; ++y) {

            for (int x = 0; x < HMR; ++x) {

                Vector2 thisLocation = new Vector2(x, y);
                List<Vector2> neighbours = GenerateNeighbours(thisLocation, HMR, HMR);

                foreach (Vector2 n in neighbours) {

                    if (heightMap[x, y] > heightMap[(int)n.x, (int)n.y] + erosionStrength) {

                        float currentHeight = heightMap[x, y];
                        heightMap[x, y] -= currentHeight * erosionAmount;
                        heightMap[(int)n.x, (int)n.y] += currentHeight * erosionAmount;

                    }
                }
            }
        }
        terrainData.SetHeights(0, 0, heightMap);
    }

    private void Tidal() {

        float[,] heightMap = terrainData.GetHeights(0, 0, HMR, HMR);

        for (int y = 0; y < HMR; ++y) {

            for (int x = 0; x < HMR; ++x) {

                Vector2 thisLocation = new Vector2(x, y);
                List<Vector2> neighbours = GenerateNeighbours(thisLocation, HMR, HMR);

                foreach (Vector2 n in neighbours) {

                    if (heightMap[x, y] < waterHeight && heightMap[(int)n.x, (int)n.y] > waterHeight) {

                        heightMap[x, y] = waterHeight;
                        heightMap[(int)n.x, (int)n.y] = waterHeight;

                    }
                }
            }
        }
        terrainData.SetHeights(0, 0, heightMap);
    }

    private void River() {


        float[,] heightMap = terrainData.GetHeights(0, 0, HMR, HMR);
        float[,] erosionMap = new float[HMR, HMR];

        for (int i = 0; i < droplets; ++i) {

            Vector2 dropletPosition = new Vector2(UnityEngine.Random.Range(0, HMR), UnityEngine.Random.Range(0, HMR));
            erosionMap[(int)dropletPosition.x, (int)dropletPosition.y] = erosionStrength;

            for (int j = 0; j < springsPerRiver; ++j) {

                erosionMap = RunRiver(dropletPosition, heightMap, erosionMap, HMR);
            }
        }
        for (int y = 0; y < HMR; ++y) {

            for (int x = 0; x < HMR; ++x) {

                if (erosionMap[x, y] > 0.0f) {

                    heightMap[x, y] -= erosionMap[x, y];
                }
            }
        }

        terrainData.SetHeights(0, 0, heightMap);
    }

    private static System.Random rng = new System.Random();
    private float[,] RunRiver(Vector2 dropletPosition, float[,] heightMap, float[,] erosionMap, int hmr) {

        while (erosionMap[(int)dropletPosition.x, (int)dropletPosition.y] > 0) {

            List<Vector2> origNeighbours = GenerateNeighbours(dropletPosition, hmr, hmr);
            var neighbours = origNeighbours.OrderBy(a => rng.Next()).ToList();
            // neighbours = rndNeighbours;
            // neighbours.Shuffle();
            bool foundLower = false;

            foreach (Vector2 n in neighbours) {

                if (heightMap[(int)n.x, (int)n.y] < heightMap[(int)dropletPosition.x, (int)dropletPosition.y]) {

                    erosionMap[(int)n.x, (int)n.y] = erosionMap[(int)dropletPosition.x, (int)dropletPosition.y] - solubilty;
                    dropletPosition = n;
                    foundLower = true;
                    break;
                }
            }
            if (!foundLower) {

                erosionMap[(int)dropletPosition.x, (int)dropletPosition.y] -= solubilty;
            }
        }
        return erosionMap;
    }

    private void Wind() {

        float[,] heightMap = terrainData.GetHeights(0, 0, HMR, HMR);

        float windDir = 50.0f;
        float sinAngle = -Mathf.Sin(Mathf.Deg2Rad * windDir);
        float cosAngle = Mathf.Cos(Mathf.Deg2Rad * windDir);

        for (int y = -(HMR - 1) * 2; y < HMR * 2; y += 10) {

            for (int x = -(HMR - 1) * 2; x < HMR * 2; x += 1) {

                float thisNoise = (float)Mathf.PerlinNoise(x * 0.06f, y * 0.06f) * 20.0f * erosionStrength;
                int nX = x;
                int digY = y + (int)thisNoise;
                int nY = y + 5 + (int)thisNoise;

                Vector2 digCoords = new Vector2(x * cosAngle - digY * sinAngle, digY * cosAngle + x * sinAngle);
                Vector2 pileCoords = new Vector2(nX * cosAngle - nY * sinAngle, nY * cosAngle + nX * sinAngle);

                if (!(pileCoords.x < 0 || pileCoords.x > (HMR - 1) ||
                    pileCoords.y < 0 || pileCoords.y > (HMR - 1) ||
                    (int)digCoords.x < 0 || (int)digCoords.x > (HMR - 1) ||
                    (int)digCoords.y < 0 || (int)digCoords.y > (HMR - 1))) {
                    //Debug.Log("X: " + x + " Y: " + y);
                    heightMap[(int)digCoords.x, (int)digCoords.y] -= 0.001f;
                    heightMap[(int)pileCoords.x, (int)pileCoords.y] += 0.001f;
                }
            }
        }
        terrainData.SetHeights(0, 0, heightMap);
    }

    float[,] tempHeightMap;
    private void DigCanyon() {

        float digDepth = 0.05f;
        float bankSlope = 0.001f;
        float maxDepth = 0.0f;

        tempHeightMap = terrainData.GetHeights(0, 0, HMR, HMR);

        int cX = 1;
        int cY = UnityEngine.Random.Range(10, HMR - 10);

        while (cY >= 0 && cY < HMR && cX > 0 && cX < HMR) {

            CanyonCrawler(cX, cY, tempHeightMap[cX, cY] - digDepth, bankSlope, maxDepth);
            cX = cX + UnityEngine.Random.Range(1, 3);
            cY = cY + UnityEngine.Random.Range(-2, 3);
        }
        terrainData.SetHeights(0, 0, tempHeightMap);
    }

    void CanyonCrawler(int x, int y, float height, float slope, float maxDepth) {

        if (x < 0 || x >= HMR) return;              // Off x range of map
        if (y < 0 || y >= HMR) return;              // Off y range of map
        if (height <= maxDepth) return;             // Has hit lowest point
        if (tempHeightMap[x, y] <= height) return;  // Has run into lower elevation

        tempHeightMap[x, y] = height;

        CanyonCrawler(x + 1, y, height + UnityEngine.Random.Range(slope, slope + 0.01f), slope, maxDepth);
        CanyonCrawler(x - 1, y, height + UnityEngine.Random.Range(slope, slope + 0.01f), slope, maxDepth);
        CanyonCrawler(x + 1, y + 1, height + UnityEngine.Random.Range(slope, slope + 0.01f), slope, maxDepth);
        CanyonCrawler(x - 1, y + 1, height + UnityEngine.Random.Range(slope, slope + 0.01f), slope, maxDepth);
        CanyonCrawler(x, y - 1, height + UnityEngine.Random.Range(slope, slope + 0.01f), slope, maxDepth);
        CanyonCrawler(x, y + 1, height + UnityEngine.Random.Range(slope, slope + 0.01f), slope, maxDepth);
    }

    // Vegetation *********************************************
    [System.Serializable]
    public class Vegetation {

        public GameObject mesh;
        public float minHeight = 0.1f;
        public float maxHeight = 0.2f;
        public float minSlope = 0.0f;
        public float maxSlope = 90.0f;
        public float minScale = 0.5f;
        public float maxScale = 1.0f;
        public Color colour1 = Color.white;
        public Color colour2 = Color.white;
        public Color lightColour = Color.white;
        public float minRotation = 0.0f;
        public float maxRotation = 360.0f;
        public float density = 0.5f;
        public bool remove = false;
    }
    public List<Vegetation> vegetation = new List<Vegetation>() {

        new Vegetation()
    };

    public int maxTrees = 5000;
    public int treeSpacing = 5;

    // Details ************************************************
    [System.Serializable]
    public class Detail {

        public GameObject prototype = null;
        public Texture2D prototypeTexture = null;
        public float minHeight = 0.1f;
        public float maxHeight = 0.2f;
        public float minSlope = 0.0f;
        public float maxSlope = 1.0f;
        public Color dryColor = Color.white;
        public Color healthyColor = Color.white;
        public Vector2 heightRange = new Vector2(1.0f, 1.0f);
        public Vector2 widthRange = new Vector2(1.0f, 1.0f);
        public float noiseSpread = 0.5f;
        public float overlap = 0.01f;
        public float feather = 0.05f;
        public float density = 0.5f;
        public bool remove = false;
    }

    public List<Detail> details = new List<Detail>() {

        new Detail()
    };

    public int maxDetails = 5000;
    public int detailSpacing = 5;

    public void AddDetails() {

        DetailPrototype[] newDetailPrototypes;
        newDetailPrototypes = new DetailPrototype[details.Count];
        int dIndex = 0;
        float[,] heightMap = terrainData.GetHeights(0, 0, HMR, HMR);

        foreach (Detail d in details) {

            newDetailPrototypes[dIndex] = new DetailPrototype {

                prototype = d.prototype,
                prototypeTexture = d.prototypeTexture,
                healthyColor = Color.white,
                dryColor = d.dryColor,
                minHeight = d.heightRange.x,
                maxHeight = d.heightRange.y,
                minWidth = d.widthRange.x,
                maxWidth = d.widthRange.y,
                noiseSpread = d.noiseSpread
            };

            if (newDetailPrototypes[dIndex].prototype) {

                newDetailPrototypes[dIndex].usePrototypeMesh = true;
                newDetailPrototypes[dIndex].renderMode = DetailRenderMode.Grass;
            }
            else {

                newDetailPrototypes[dIndex].usePrototypeMesh = false;
                newDetailPrototypes[dIndex].renderMode = DetailRenderMode.GrassBillboard;
            }
            dIndex++;
        }
        terrainData.detailPrototypes = newDetailPrototypes;

        float minDetailMapValue = 0.0f;
        float maxDetailMapValue = 16.0f;

        if (terrainData.detailScatterMode == DetailScatterMode.CoverageMode) maxDetailMapValue = 255.0f;

        for (int i = 0; i < terrainData.detailPrototypes.Length; ++i) {

            int[,] detailMap = new int[terrainData.detailWidth, terrainData.detailHeight];

            for (int y = 0; y < terrainData.detailHeight; y += detailSpacing) {

                for (int x = 0; x < terrainData.detailWidth; x += detailSpacing) {

                    if (UnityEngine.Random.Range(0.0f, 1.0f) > details[i].density) continue;
                    int xHM = (int)(x / (float)terrainData.detailWidth * HMR);
                    int yHM = (int)(y / (float)terrainData.detailHeight * HMR);

                    float thisNoise = Utils.Map(Mathf.PerlinNoise(x * details[i].feather,
                                                y * details[i].feather), 0, 1, 0.5f, 1);
                    float thisHeightStart = details[i].minHeight * thisNoise -
                                            details[i].overlap * thisNoise;
                    float nextHeightStart = details[i].maxHeight * thisNoise +
                                            details[i].overlap * thisNoise;

                    float thisHeight = heightMap[yHM, xHM];
                    float steepness = terrainData.GetSteepness(xHM / (float)terrainData.size.x,
                                                                yHM / (float)terrainData.size.z);
                    if ((thisHeight >= thisHeightStart && thisHeight <= nextHeightStart) &&
                        (steepness >= details[i].minSlope && steepness <= details[i].maxSlope)) {
                        detailMap[y, x] = (int)UnityEngine.Random.Range(minDetailMapValue, maxDetailMapValue);
                    }
                }
            }
            terrainData.SetDetailLayer(0, 0, i, detailMap);
        }
    }

    public void AddNewDetails() {

        details.Add(new Detail());
    }

    public void RemoveDetails() {

        //for (int i = details.Count - 1; i >= 1; i--)
        //    if (details[i].remove) details.RemoveAt(i);
        List<Detail> keptDetail = new List<Detail>();
        for (int i = 0; i < vegetation.Count; ++i) {

            if (!vegetation[i].remove) {

                keptDetail.Add(details[i]);
            }
        }
        if (keptDetail.Count == 0) //don't want to keep any
        {
            keptDetail.Add(details[0]); //add at least 1
        }
        details = keptDetail;
    }

    // Splatmaps **********************************************
    [System.Serializable]
    public class SplatHeights {

        public Texture2D texture = null;
        public Texture2D textureNormalMap = null;
        public float minHeight = 0.1f;
        public float maxHeight = 0.2f;
        public float minSlope = 0.0f;
        public float maxSlope = 90.0f;
        public Vector2 tileOffset = Vector2.zero;
        public Vector2 tileSize = new Vector2(50.0f, 50.0f);

        public float splatOffset = 000000.10f;
        public float splatNoiseXScale = 0.01f;
        public float splatNoiseYScale = 0.01f;
        public float splatNoiseZScale = 0.10f;
        public bool remove = false;
    }
    public List<SplatHeights> splatHeights = new List<SplatHeights>() {

        new SplatHeights()
    };

    // Perlin Noise ***********************************************
    public float perlinXScale = 0.01f;
    public float perlinYScale = 0.01f;
    public int perlinOffsetX = 0;
    public int perlinOffsetY = 0;
    public int perlinOctaves = 3;
    public float perlinPersistance = 8.0f;
    public float perlinHeightScale = 0.009f;

    // Multiple Perlin Noise ***********************************************
    // Multiple Perlin Noise *************************************
    [System.Serializable]
    public class PerlinParameters {

        public float mPerlinXScale = 0.01f;
        public float mPerlinYScale = 0.01f;
        public int mPerlinOctaves = 3;
        public float mPerlinPersistance = 8;
        public float mPerlinHeightScale = 0.09f;
        public int mPerlinOffsetX = 0;
        public int mPerlinOffsetY = 0;
        public bool remove = false;
    }

    public List<PerlinParameters> perlinParameters = new List<PerlinParameters>() {

        new PerlinParameters()
    };

    // Voronoi ************************************************
    public float voronoiFallOff = 0.2f;
    public float voronoiDropOff = 0.6f;
    public float voronoiMinHeight = 0.1f;
    public float voronoiMaxHeight = 0.5f;
    public int voronoiPeaks = 5;
    public VoronoiType voronoiType = VoronoiType.Linear;
    public enum VoronoiType {

        Linear,
        Power,
        SinPow,
        Combined,
        Perlin
    }

    // Midpoint Displacement *********************************
    public float MPDHeightMin = -2.0f;
    public float MPDHeightMax = 2.0f;
    public float MPDHeightDampner = 2.0f;
    public float MPDRoughness = 2.0f;

    public int smoothAmount = 1;

    // Water Level
    public float waterHeight = 0.5f;
    public GameObject waterGO;

    public void AddWater() {

        GameObject water = GameObject.Find("water");
        if (!water) {

            water = Instantiate(waterGO, this.transform.position, this.transform.rotation);
            water.name = "water";
        }
        water.transform.position = this.transform.position +
            new Vector3(terrainData.size.x / 2.0f,
            waterHeight * terrainData.size.y,
            terrainData.size.z / 2.0f);

        water.transform.localScale = new Vector3(terrainData.size.x, 1.0f, terrainData.size.z);
    }

    public void PlantVegetation() {

        TreePrototype[] newTreePrototypes;
        newTreePrototypes = new TreePrototype[vegetation.Count];
        int tIndex = 0;
        foreach (Vegetation t in vegetation) {

            newTreePrototypes[tIndex] = new TreePrototype {
                prefab = t.mesh
            };
            tIndex++;
        }
        terrainData.treePrototypes = newTreePrototypes;

        List<TreeInstance> allVegetation = new List<TreeInstance>();
        int tah = terrainData.alphamapHeight;
        int taw = terrainData.alphamapWidth;

        for (int z = 0; z < tah; z += treeSpacing) {

            for (int x = 0; x < taw; x += treeSpacing) {

                for (int tp = 0; tp < terrainData.treePrototypes.Length; ++tp) {

                    if (UnityEngine.Random.Range(0.0f, 1.0f) > vegetation[tp].density) break;

                    float thisHeight = terrainData.GetHeight(x, z) / terrainData.size.y;
                    float thisHeightStart = vegetation[tp].minHeight;
                    float thisHeightEnd = vegetation[tp].maxHeight;

                    float normX = x * 1.0f / (taw - 1.0f);
                    float normY = z * 1.0f / (tah - 1.0f);
                    float steepness = terrainData.GetSteepness(x, z);

                    if ((thisHeight >= thisHeightStart && thisHeight <= thisHeightEnd) &&
                        (steepness >= vegetation[tp].minSlope && steepness <= vegetation[tp].maxSlope)) {

                        TreeInstance instance = new TreeInstance();
                        instance.position = new Vector3((x + UnityEngine.Random.Range(-5.0f, 5.0f)) / taw,
                                                            thisHeight,
                                                            (z + UnityEngine.Random.Range(-5.0f, 5.0f)) / tah);

                        Vector3 treeWorldPos = new Vector3(instance.position.x * terrainData.size.x,
                                                                instance.position.y * terrainData.size.y,
                                                                instance.position.z * terrainData.size.z) + this.transform.position;

                        RaycastHit hit;
                        int layerMask = 1 << terrainLayer;
                        if (Physics.Raycast(treeWorldPos + new Vector3(0.0f, 10.0f, 0.0f), -Vector3.up, out hit, 100, layerMask) ||
                            Physics.Raycast(treeWorldPos - new Vector3(0.0f, 10.0f, 0.0f), Vector3.up, out hit, 100, layerMask)) {

                            float treeHeight = (hit.point.y - this.transform.position.y) / terrainData.size.y;
                            instance.position = new Vector3(instance.position.x,
                                                            treeHeight,
                                                            instance.position.z);
                        }

                        instance.rotation = UnityEngine.Random.Range(vegetation[tp].minRotation,
                                                                 vegetation[tp].maxRotation);
                        instance.prototypeIndex = tp;
                        instance.color = Color.Lerp(vegetation[tp].colour1,
                                                    vegetation[tp].colour2,
                                                    UnityEngine.Random.Range(0.0f, 1.0f));
                        instance.lightmapColor = vegetation[tp].lightColour;
                        float s = UnityEngine.Random.Range(vegetation[tp].minScale, vegetation[tp].maxScale);
                        instance.heightScale = s;
                        instance.widthScale = s;


                        allVegetation.Add(instance);
                        if (allVegetation.Count >= maxTrees) goto TREESDONE;
                    }
                }
            }
        }
    TREESDONE:
        terrainData.treeInstances = allVegetation.ToArray();
    }

    public void AddNewVegetation() {

        vegetation.Add(new Vegetation());
    }

    public void RemoveVegetation() {

        //for (int i = vegetation.Count - 1; i >= 1; i--)
        //    if (vegetation[i].remove) vegetation.RemoveAt(i);


        List<Vegetation> keptvegetation = new List<Vegetation>();
        for (int i = 0; i < vegetation.Count; ++i) {

            if (!vegetation[i].remove) {

                keptvegetation.Add(vegetation[i]);
            }
        }
        if (keptvegetation.Count == 0) //don't want to keep any
        {
            keptvegetation.Add(vegetation[0]); //add at least 1
        }
        vegetation = keptvegetation;
    }

    public void SplatMaps() {

        int tah = terrainData.alphamapHeight;
        int taw = terrainData.alphamapWidth;
        int aml = terrainData.alphamapLayers;

        TerrainLayer[] newSplatPrototypes;

        newSplatPrototypes = new TerrainLayer[splatHeights.Count];
        int spIndex = 0;

        foreach (SplatHeights sh in splatHeights) {

            newSplatPrototypes[spIndex] = new TerrainLayer {

                diffuseTexture = sh.texture,
                normalMapTexture = sh.textureNormalMap,
                tileOffset = sh.tileOffset,
                tileSize = sh.tileSize
            };

            newSplatPrototypes[spIndex].diffuseTexture.Apply(true);
            string path = "Assets/New Terrain Layer " + spIndex + ".terrainlayer";
            AssetDatabase.CreateAsset(newSplatPrototypes[spIndex], path);
            spIndex++;
            Selection.activeObject = this.gameObject;
        }
        terrainData.terrainLayers = newSplatPrototypes;

        //USE HEIGHTMAP RESOLUTION HERE
        float[,] heightMap = terrainData.GetHeights(0, 0, HMR, HMR);

        //USE SPLATMAP RESOLUTUON HERE
        float[,,] splatmapData = new float[taw, tah, aml];

        for (int y = 0; y < tah; ++y) {

            for (int x = 0; x < tah; ++x) {

                float[] splat = new float[aml];
                bool emptySplat = true;

                for (int i = 0; i < splatHeights.Count; ++i) {

                    float noise = Mathf.PerlinNoise(x * splatHeights[i].splatNoiseXScale,
                                                    y * splatHeights[i].splatNoiseYScale) *
                                                    splatHeights[i].splatNoiseZScale;

                    float offset = splatHeights[i].splatOffset + noise;
                    float thisHeightStart = splatHeights[i].minHeight - offset;
                    float thisHeightStop = splatHeights[i].maxHeight + offset;

                    //SCALE FOR RESOLUTION DIFFERENCES
                    //NOTE: The switching of the x and y is no longer needed here
                    //Scale between the heightmap resolution and the splatmap resolution
                    int hmX = y * ((HMR - 1) / tah);
                    int hmY = y * ((HMR - 1) / tah);

                    float normX = x * 1.0f / (terrainData.alphamapWidth - 1);
                    float normY = y * 1.0f / (terrainData.alphamapHeight - 1);

                    // Get the steepness value at the normalised coordinate
                    float steepness = terrainData.GetSteepness(normX, normY);

                    if ((heightMap[hmX, hmY] >= thisHeightStart && heightMap[hmX, hmY] <= thisHeightStop) &&
                          (steepness >= splatHeights[i].minSlope && steepness <= splatHeights[i].maxSlope)) {

                        if (heightMap[hmX, hmY] <= splatHeights[i].minHeight)
                            splat[i] = 1.0f - Mathf.Abs(heightMap[hmX, hmY] - splatHeights[i].minHeight) / offset;
                        else if (heightMap[hmX, hmY] >= splatHeights[i].maxHeight)
                            splat[i] = 1.0f - Mathf.Abs(heightMap[hmX, hmY] - splatHeights[i].maxHeight) / offset;
                        else
                            splat[i] = 1;

                        emptySplat = false;
                    }

                }

                NormalizeVector(ref splat);

                if (emptySplat) {

                    splatmapData[x, y, 0] = 1;
                }
                else {

                    for (int j = 0; j < splatHeights.Count; j++) {
                        splatmapData[y, x, j] = splat[j];
                    }
                }
            }
        }
        terrainData.SetAlphamaps(0, 0, splatmapData);
    }

    public void AddNewSplatHeight() {

        splatHeights.Add(new SplatHeights());
    }

    public void RemoveSplatHeights() {

        List<SplatHeights> keptSplatHeights = new List<SplatHeights>();
        for (int i = 0; i < splatHeights.Count; ++i) {
            if (!splatHeights[i].remove) {
                keptSplatHeights.Add(splatHeights[i]);
            }
        }
        if (keptSplatHeights.Count == 0) //don't want to keep any
        {
            keptSplatHeights.Add(splatHeights[0]); //add at least 1
        }
        splatHeights = keptSplatHeights;
    }

    public static void NormalizeVector(ref float[] v) {

        float total = 0.0f;
        for (int i = 0; i < v.Length; ++i) {

            total += v[i];
        }
        if (total == 0) return;
        for (int i = 0; i < v.Length; ++i) {

            v[i] /= total;
        }
    }



    public void Perlin() {

        float[,] heightMap = GetHeightMap();
        for (int y = 0; y < HMR; ++y) {


            for (int x = 0; x < HMR; ++x)

                heightMap[x, y] += Utils.fBM((x + perlinOffsetX) * perlinXScale,
                                            (y + perlinOffsetY) * perlinYScale,
                                            perlinOctaves,
                                            perlinPersistance) * perlinHeightScale;
        }
        terrainData.SetHeights(0, 0, heightMap);
    }

    public void MultiplePerlinTerrain() {

        float[,] heightMap = GetHeightMap();
        for (int y = 0; y < HMR; ++y) {

            for (int x = 0; x < HMR; ++x) {

                foreach (PerlinParameters p in perlinParameters) {

                    heightMap[x, y] += Utils.fBM((x + p.mPerlinOffsetX) * p.mPerlinXScale,
                                                (y + p.mPerlinOffsetY) * p.mPerlinYScale,
                                                p.mPerlinOctaves,
                                                p.mPerlinPersistance) * p.mPerlinHeightScale;
                }
            }
        }
        terrainData.SetHeights(0, 0, heightMap);
    }
    public void AddNewPerlin() {

        perlinParameters.Add(new PerlinParameters());
    }

    public void RemovePerlin() {

        for (int i = perlinParameters.Count - 1; i >= 1; --i)
            if (perlinParameters[i].remove) perlinParameters.RemoveAt(i);
    }

    public void RandomTerrain() {

        //int hmr = terrainData.heightmapResolution;
        float[,] heightMap = GetHeightMap();

        for (int x = 0; x < HMR; ++x) {
            for (int z = 0; z < HMR; ++z) {

                heightMap[x, z] += UnityEngine.Random.Range(randomHeightRange.x, randomHeightRange.y);
            }
        }
        terrainData.SetHeights(0, 0, heightMap);
    }

    public void LoadTexture() {

        //float[,] heightMap = new float[hmr, hmr]; //terrainData.GetHeights(0, 0, hmr, hmr);
        float[,] heightMap = GetHeightMap();

        for (int x = 0; x < HMR; ++x) {
            for (int z = 0; z < HMR; ++z) {

                heightMap[x, z] += heightMapImage.GetPixel((int)(x * heightMapScale.x),
                    (int)(z * heightMapScale.z)).grayscale * heightMapScale.y;
            }
        }
        terrainData.SetHeights(0, 0, heightMap);
    }

    public void RidgeNoise() {

        //ResetTerrain();
        MultiplePerlinTerrain();
        float[,] heightMap = GetHeightMap();

        for (int y = 0; y < HMR; ++y) {

            for (int x = 0; x < HMR; ++x) {

                heightMap[x, y] = 1 - Mathf.Abs(heightMap[x, y] - 0.5f);
            }
        }
        isRidged = !isRidged;
        terrainData.SetHeights(0, 0, heightMap);
    }

    public void Voronoi() {

        float[,] heightMap = GetHeightMap();

        for (int p = 0; p < voronoiPeaks; ++p) {

            Vector3 peak = new Vector3(UnityEngine.Random.Range(0, HMR),
                                       UnityEngine.Random.Range(voronoiMinHeight, voronoiMaxHeight),
                                       UnityEngine.Random.Range(0, HMR));

            if (heightMap[(int)peak.x, (int)peak.z] < peak.y) {

                heightMap[(int)peak.x, (int)peak.z] = peak.y;
            }
            else {

                continue;
            }

            Vector2 peakLocation = new Vector2(peak.x, peak.z);
            float maxDistance = Vector2.Distance(new Vector2(0.0f, 0.0f), new Vector2(HMR, HMR));

            for (int y = 0; y < HMR; ++y) {

                for (int x = 0; x < HMR; ++x) {

                    if (!(x == peak.x && y == peak.z)) {

                        float distanceToPeak = Vector2.Distance(peakLocation, new Vector2(x, y)) / maxDistance;
                        float h;

                        if (voronoiType == VoronoiType.Combined) {

                            h = peak.y - distanceToPeak * voronoiFallOff -
                                MathF.Pow(distanceToPeak, voronoiDropOff); // Combined
                        }
                        else if (voronoiType == VoronoiType.Power) {

                            h = peak.y - Mathf.Pow(distanceToPeak, voronoiDropOff) * voronoiFallOff;    // Power
                        }
                        else if (voronoiType == VoronoiType.SinPow) {

                            h = peak.y - Mathf.Pow(distanceToPeak * 3.0f, voronoiFallOff) -
                                Mathf.Sin(distanceToPeak * 2.0f * Mathf.PI) / voronoiDropOff;   // SinPow
                        }
                        else if (voronoiType == VoronoiType.Perlin) {

                            h = (peak.y - distanceToPeak * voronoiFallOff) + Utils.fBM((x + perlinOffsetX) * perlinXScale,
                                            (y + perlinOffsetY) * perlinYScale,
                                            perlinOctaves,
                                            perlinPersistance) * perlinHeightScale;    // Perlin
                        }
                        else {

                            h = (peak.y - distanceToPeak * voronoiFallOff);   // Linear
                        }

                        if (heightMap[x, y] < h) {

                            heightMap[x, y] = h;
                        }
                    }
                }
            }
        }

        terrainData.SetHeights(0, 0, heightMap);
    }

    public void MidPointDisplacement() {

        float[,] heightMap = GetHeightMap();
        int width = HMR - 1;
        int squareSize = width;
        float heightMin = MPDHeightMin;
        float heightMax = MPDHeightMax;
        float heightDampener = (float)Mathf.Pow(MPDHeightDampner, -1 * MPDRoughness);

        int cornerX, cornerY;
        int midX, midY;
        int pmidXL, pmidXR, pmidYU, pmidYD;

        //heightMap[0, 0] = UnityEngine.Random.Range(0.0f, 0.2f);
        //heightMap[0, hmr - 2] = UnityEngine.Random.Range(0.0f, 0.2f);
        //heightMap[hmr - 2, 0] = UnityEngine.Random.Range(0.0f, 0.2f);
        //heightMap[hmr - 2, hmr - 2] = UnityEngine.Random.Range(0.0f, 0.2f);

        while (squareSize > 0) {

            for (int x = 0; x < width; x += squareSize) {

                for (int y = 0; y < width; y += squareSize) {

                    cornerX = (x + squareSize);
                    cornerY = (y + squareSize);

                    midX = (int)(x + squareSize / 2.0f);
                    midY = (int)(y + squareSize / 2.0f);

                    heightMap[midX, midY] = (float)((heightMap[x, y] +
                                                     heightMap[cornerX, y] +
                                                     heightMap[x, cornerY] +
                                                     heightMap[cornerX, cornerY]) / 4.0f +
                                                     UnityEngine.Random.Range(heightMin, heightMax));
                }
            }

            for (int x = 0; x < width; x += squareSize) {

                for (int y = 0; y < width; y += squareSize) {

                    cornerX = (x + squareSize);
                    cornerY = (y + squareSize);

                    midX = (int)(x + squareSize / 2.0f);
                    midY = (int)(y + squareSize / 2.0f);

                    pmidXR = (int)(midX + squareSize);
                    pmidYU = (int)(midY + squareSize);
                    pmidXL = (int)(midX - squareSize);
                    pmidYD = (int)(midY - squareSize);

                    if (pmidXL <= 0 || pmidYD <= 0
                        || pmidXR >= width - 1 || pmidYU >= width - 1) continue;

                    // Calculate the square value for the bottom right
                    heightMap[midX, y] = (float)((heightMap[midX, midY] +
                                                  heightMap[x, y] +
                                                  heightMap[midX, pmidYD] +
                                                  heightMap[cornerX, y]) / 4.0f +
                                                  UnityEngine.Random.Range(heightMin, heightMax));

                    // Calculate the square value for the top side
                    heightMap[midX, cornerY] = (float)((heightMap[x, cornerY] +
                                                  heightMap[midX, midY] +
                                                  heightMap[cornerX, cornerY] +
                                                  heightMap[midX, pmidYU]) / 4.0f +
                                                  UnityEngine.Random.Range(heightMin, heightMax));

                    // Calculate the square value for the left side
                    heightMap[x, midY] = (float)((heightMap[x, y] +
                                                  heightMap[pmidXL, midY] +
                                                  heightMap[x, cornerY] +
                                                  heightMap[midX, midY]) / 4.0f +
                                                  UnityEngine.Random.Range(heightMin, heightMax));

                    // Calculate the square value for the right side
                    heightMap[cornerX, midY] = (float)((heightMap[midX, y] +
                                                  heightMap[midX, midY] +
                                                  heightMap[cornerX, cornerY] +
                                                  heightMap[pmidXR, midY]) / 4.0f +
                                                  UnityEngine.Random.Range(heightMin, heightMax));
                }
            }

            squareSize = (int)(squareSize / 2.0f);
            heightMin *= heightDampener;
            heightMax *= heightDampener;
        }
        terrainData.SetHeights(0, 0, heightMap);
    }

    List<Vector2> GenerateNeighbours(Vector2 pos, int width, int height) {

        List<Vector2> neighbours = new List<Vector2>();

        for (int y = -1; y < 2; ++y) {

            for (int x = -1; x < 2; ++x) {

                if (!(x == 0 && y == 0)) {

                    Vector2 nPos = new Vector2(
                        Mathf.Clamp(pos.x + x, 0.0f, width - 1),
                        Mathf.Clamp(pos.y + y, 0.0f, height - 1));

                    if (!neighbours.Contains(nPos))
                        neighbours.Add(nPos);
                }
            }
        }
        return neighbours;
    }

    public void Smooth() {

        float[,] heightMap = terrainData.GetHeights(0, 0, HMR, HMR);
        float smoothProgress = 0.0f;
        EditorUtility.DisplayProgressBar("Smoothing Terrain", "Progress", smoothProgress);

        for (int s = 0; s < smoothAmount; ++s) {

            for (int y = 0; y < HMR; ++y) {

                for (int x = 0; x < HMR; ++x) {

                    float avgHeight = heightMap[x, y];
                    List<Vector2> neighbours = GenerateNeighbours(new Vector2(x, y), HMR, HMR);

                    foreach (Vector2 n in neighbours) {

                        avgHeight += heightMap[(int)n.x, (int)n.y];
                    }
                    heightMap[x, y] = avgHeight / ((float)neighbours.Count + 1);
                }
            }
            smoothProgress++;
            EditorUtility.DisplayProgressBar("Smoothing Terrain", "Progress", smoothProgress / smoothAmount);
        }
        terrainData.SetHeights(0, 0, heightMap);
        EditorUtility.ClearProgressBar();
    }


    public float[,] GetHeightMap() {

        if (!resetTerrain) {

            return terrainData.GetHeights(0, 0, HMR, HMR);
        }
        else {

            return new float[HMR, HMR];
        }
    }

    public void ResetTerrain() {

        float[,] heightMap = new float[HMR, HMR];
        terrainData.SetHeights(0, 0, heightMap);
    }

    private void OnEnable() {

        Debug.Log("Initialising Tertain Data");
        //Debug.Log(terrainData.size);
        //terrainData.size;
        terrain = this.GetComponent<Terrain>();
        terrainData = Terrain.activeTerrain.terrainData;
    }

    public enum TagType { Tag, Layer };
    [SerializeField]
    int terrainLayer = 0;

    private void Start() {

        SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty tagsProp = tagManager.FindProperty("tags");

        AddTag(tagsProp, "Terrain", TagType.Tag);
        AddTag(tagsProp, "Cloud", TagType.Tag);
        AddTag(tagsProp, "Shore", TagType.Tag);

        // Apply tag changes to tag database
        tagManager.ApplyModifiedProperties();

        SerializedProperty layerProp = tagManager.FindProperty("layers");
        AddTag(layerProp, "Sky", TagType.Layer);
        terrainLayer = AddTag(layerProp, "Terrain", TagType.Layer);
        tagManager.ApplyModifiedProperties();

        // Tag this object
        this.gameObject.tag = "Terrain";
        this.gameObject.layer = terrainLayer;
    }

    int AddTag(SerializedProperty tagsProp, string newTag, TagType tType) {

        bool found = false;

        // Ensure the tag doesn't already exist
        for (int i = 0; i < tagsProp.arraySize; ++i) {

            SerializedProperty t = tagsProp.GetArrayElementAtIndex(i);
            if (t.stringValue.Equals(newTag)) {

                found = true;
                return i;
            }
        }

        // Add your new tag
        if (!found && tType == TagType.Tag) {

            tagsProp.InsertArrayElementAtIndex(0);
            SerializedProperty newTagProp = tagsProp.GetArrayElementAtIndex(0);
            newTagProp.stringValue = newTag;
        }

        // Add new layer
        else if (!found && tType == TagType.Layer) {

            for (int j = 8; j < tagsProp.arraySize; ++j) {

                SerializedProperty newLayer = tagsProp.GetArrayElementAtIndex(j);
                // Add layer in next slot
                if (newLayer.stringValue == "") {

                    Debug.Log("Adding New Layer: " + newTag);
                    newLayer.stringValue = newTag;
                    return j;
                }
            }
        }
        return -1;
    }
}

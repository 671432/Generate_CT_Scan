using UnityEngine;
using UnityEngine.UIElements;
using System.Linq;
using System;
using System.Collections.Generic;
using System.IO;

public class quadScript : MonoBehaviour
{

    // Dicom har et "levende" dictionary som leses fra xml ved initDicom
    // slices må sorteres, og det basert på en tag, men at pixeldata lesing er en separat operasjon, derfor har vi nullpeker til pixeldata
    // dicomfile lagres slik at fil ikke må leses enda en gang når pixeldata hentes

    // member variables of quadScript, accessible from any function
    Slice[] _slices;
    private List<Vector3> _vertices = new();
    private List<int> _indices = new();
    private int _index;
    int _numSlices;
    int _minIntensity;
    int _maxIntensity;


    private Button _button1;
    private Button _button2;
    private Button _button3;
    private Button _button4;
    private Button _button5;
    private Toggle _toggle1;
    private Slider _slider1;
    private Slider _slider2;
    private Slider _slider3;
    private Slider _slider4;
    //int _iso;

    public int textureSize = 512;
    public int radius = 256;

    private int circleType;

    // Use this for initialization
    void Start()
    {
        var uiDocument = GameObject.Find("MyUIDocument").GetComponent<UIDocument>();
        _button1 = uiDocument.rootVisualElement.Q("button1") as Button;
        _button2 = uiDocument.rootVisualElement.Q("button2") as Button;
        _button3 = uiDocument.rootVisualElement.Q("button3") as Button;
        _button4 = uiDocument.rootVisualElement.Q("button4") as Button;
        _button5 = uiDocument.rootVisualElement.Q("button5") as Button;
        _toggle1 = uiDocument.rootVisualElement.Q("toggle1") as Toggle;
        _slider1 = uiDocument.rootVisualElement.Q("slider1") as Slider;
        _slider2 = uiDocument.rootVisualElement.Q("slider2") as Slider;
        _slider3 = uiDocument.rootVisualElement.Q("slider3") as Slider;
        _slider4 = uiDocument.rootVisualElement.Q("slider4") as Slider;

        // Register callbacks
        _button1.RegisterCallback<ClickEvent>(button1Pushed);
        _button2.RegisterCallback<ClickEvent>(button2Pushed);
        _button3.RegisterCallback<ClickEvent>(button3Pushed);
        _button4.RegisterCallback<ClickEvent>(button4Pushed);
        _button5.RegisterCallback<ClickEvent>(button5Pushed);
        _slider1.RegisterValueChangedCallback(slice1SliderChange);
        _slider2.RegisterValueChangedCallback(slice2SliderChange);
        _slider3.RegisterValueChangedCallback(slice3SliderChange);
        _slider4.RegisterValueChangedCallback(slice4SliderChange);
        _toggle1.RegisterValueChangedCallback(evt =>
        {
            Debug.Log($"Toggle pressed. New value: {evt.newValue}");
        });

        Slice.initDicom();

        string dicomfilepath = Application.dataPath + @"\..\dicomdata\";
        _slices = processSlices(dicomfilepath);
        setTexture(_slices[259]);

        if (_slices == null || _slices.Length == 0)
        {
            Debug.LogError("Failed to load DICOM slices.");
            return;
        }

        // Initialize mesh object
        meshScript mscript = GameObject.Find("GameObjectMesh").GetComponent<meshScript>();
        List<Vector3> vertices = new List<Vector3>();
        List<int> indices = new List<int>();
        mscript.createMeshGeometry(vertices, indices);
    }


    /* MARCHING SQUARES */

    Vector2 InterpolateEdge(float x1, float y1, float x2, float y2, float v1, float v2, float isoLevel)
    {
        if (Mathf.Approximately(v1, v2))
            return new Vector2((x1 + x2) / 2, (y1 + y2) / 2);

        if (Mathf.Approximately(v2 - v1, 0f))
            return new Vector2(x1, y1);

        float t = (isoLevel - v1) / (v2 - v1);
        t = Mathf.Clamp01(t); // Ensure t is between 0 and 1
        return new Vector2(
            Mathf.Lerp(x1, x2, t),
            Mathf.Lerp(y1, y2, t)
        );
    }

    void GenerateMarchingSquares(float isoValue)
    {
        float adjustedIsoValue = isoValue; // Remove the Lerp to use direct iso value from slider
        Debug.Log($"IsoValue: {adjustedIsoValue}");

        List<Vector3> vertices = new List<Vector3>();
        List<int> indices = new List<int>();

        Texture2D texture = GetComponent<Renderer>().material.mainTexture as Texture2D;
        if (texture == null)
        {
            Debug.LogError("No texture found!");
            return;
        }

        int width = texture.width;
        int height = texture.height;

        // Process each cell in the grid
        for (int y = 0; y < height - 1; y++)
        {
            for (int x = 0; x < width - 1; x++)
            {
                // Get pixel intensities (grayscale values 0-1)
                float v1 = texture.GetPixel(x, y).r;           // Top-left pixel
                float v2 = texture.GetPixel(x + 1, y).r;       // Top-right pixel
                float v3 = texture.GetPixel(x, y + 1).r;       // Bottom-left pixel
                float v4 = texture.GetPixel(x + 1, y + 1).r;   // Bottom-right pixel

                // Create unique square index based on intensity threshold

                int squareIndex = 0;
                if (v1 >= adjustedIsoValue) squareIndex |= 1;
                if (v2 >= adjustedIsoValue) squareIndex |= 2;
                if (v3 >= adjustedIsoValue) squareIndex |= 4;
                if (v4 >= adjustedIsoValue) squareIndex |= 8;

                if (squareIndex == 0 || squareIndex == 15) continue;

                Vector2 p1, p2;

                switch (squareIndex)
                {
                    case 1:
                    case 14:
                        p1 = InterpolateEdge(x, y, x, y + 1, v1, v3, adjustedIsoValue);
                        p2 = InterpolateEdge(x, y, x + 1, y, v1, v2, adjustedIsoValue);
                        AddLine(vertices, indices, p1.x, p1.y, p2.x, p2.y);
                        break;
                    case 2:
                    case 13:
                        p1 = InterpolateEdge(x, y, x + 1, y, v1, v2, adjustedIsoValue);
                        p2 = InterpolateEdge(x + 1, y, x + 1, y + 1, v2, v4, adjustedIsoValue);
                        AddLine(vertices, indices, p1.x, p1.y, p2.x, p2.y);
                        break;
                    case 3:
                    case 12:
                        p1 = InterpolateEdge(x, y + 1, x, y, v3, v1, adjustedIsoValue);
                        p2 = InterpolateEdge(x + 1, y + 1, x + 1, y, v4, v2, adjustedIsoValue);
                        AddLine(vertices, indices, p1.x, p1.y, p2.x, p2.y);
                        break;
                    case 4:
                    case 11:
                        p1 = InterpolateEdge(x, y + 1, x + 1, y + 1, v3, v4, adjustedIsoValue);
                        p2 = InterpolateEdge(x, y, x, y + 1, v1, v3, adjustedIsoValue);
                        AddLine(vertices, indices, p1.x, p1.y, p2.x, p2.y);
                        break;
                    case 6:
                    case 9:
                        p1 = InterpolateEdge(x, y, x + 1, y, v1, v2, adjustedIsoValue);
                        p2 = InterpolateEdge(x, y + 1, x + 1, y + 1, v3, v4, adjustedIsoValue);
                        AddLine(vertices, indices, p1.x, p1.y, p2.x, p2.y);
                        break;
                    case 7:
                    case 8:
                        p1 = InterpolateEdge(x, y + 1, x, y, v3, v1, adjustedIsoValue);
                        p2 = InterpolateEdge(x + 1, y + 1, x + 1, y, v4, v2, adjustedIsoValue);
                        AddLine(vertices, indices, p1.x, p1.y, p2.x, p2.y);
                        break;
                    case 5:
                    case 10:
                        float centerValue = (v1 + v2 + v3 + v4) / 4f;
                        if (centerValue >= adjustedIsoValue)
                        {
                            p1 = InterpolateEdge(x, y, x, y + 1, v1, v3, adjustedIsoValue);
                            p2 = InterpolateEdge(x + 1, y, x + 1, y + 1, v2, v4, adjustedIsoValue);
                        }
                        else
                        {
                            p1 = InterpolateEdge(x, y, x + 1, y, v1, v2, adjustedIsoValue);
                            p2 = InterpolateEdge(x, y + 1, x + 1, y + 1, v3, v4, adjustedIsoValue);
                        }
                        AddLine(vertices, indices, p1.x, p1.y, p2.x, p2.y);
                        break;
                }
            }
        }

        // Use lines for marching squares
        meshScript mscript = GameObject.Find("GameObjectMesh").GetComponent<meshScript>();
        mscript.createMeshGeometry(vertices, indices, false);
    }

    void AddLine(List<Vector3> vertices, List<int> indices, float x1, float y1, float x2, float y2)
    {
        //Debug.Log($"Vertices count: {vertices.Count}");
        // converting texturecoordinates to -0.5 to 0.5
        float worldX1 = (x1 / textureSize) - 0.5f;
        float worldY1 = (y1 / textureSize) - 0.5f;
        float worldX2 = (x2 / textureSize) - 0.5f;
        float worldY2 = (y2 / textureSize) - 0.5f;

        int indexStart = vertices.Count;
        vertices.Add(new Vector3(worldX1, worldY1, 0));
        vertices.Add(new Vector3(worldX2, worldY2, 0));
        indices.Add(indexStart);
        indices.Add(indexStart + 1);

    }

    Vector2 vec2(float x, float y)
    {
        return new Vector2(x, y);
    }








    /* DRAW CIRCLES */

    void drawCircle1()
    {

        Renderer renderer = GetComponent<Renderer>();

        Texture2D texture = renderer.material.mainTexture as Texture2D;
        if (texture == null)
        {
            Debug.LogError("No texture found on the material.");
            return;
        }

        // Create a new texture based on the existing one
        Texture2D newTexture = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);
        newTexture.SetPixels(texture.GetPixels());

        // Calculate center of the texture
        int centerX = texture.width / 2;
        int centerY = texture.height / 2;

        for (int y = 0; y <= texture.height; y++)
        {
            for (int x = 0; x <= texture.width; x++)
            {
                float distance = Mathf.Sqrt((x - centerX) * (x - centerX) + (y - centerY) * (y - centerY));
                float normalizedDistance = Mathf.Clamp01(distance / radius); // Normalise to [0,1]

                Color grayscale = new Color(normalizedDistance, normalizedDistance, normalizedDistance);
                newTexture.SetPixel(x, y, grayscale);
            }
        }

        newTexture.Apply();
        GetComponent<Renderer>().material.mainTexture = newTexture;
    }

    void drawCircle2()
    {

        Renderer renderer = GetComponent<Renderer>();

        Texture2D texture = renderer.material.mainTexture as Texture2D;
        if (texture == null)
        {
            Debug.LogError("No texture found on the material.");
            return;
        }

        // Create a new texture based on the existing one
        Texture2D newTexture = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);
        newTexture.SetPixels(texture.GetPixels());

        // Calculate center of the texture
        int centerX = texture.width / 2;
        int centerY = texture.height / 2;

        // Loop through each pixel in the texture
        for (int y = 0; y <= texture.height; y++)
        {
            for (int x = 0; x <= texture.width; x++)
            {
                // Calculate the distance from the current pixel to the center
                float distance = Mathf.Sqrt(Mathf.Pow(x - centerX, 2) + Mathf.Pow(y - centerY, 2));
                //float distance = Mathf.Sqrt((x - centerX) * (x - centerX) + (y - centerY) * (y - centerY));

                // Check if the pixel is within the circle's radius
                if (distance <= radius)
                {
                    // Fill the pixel with white if it's inside the circle
                    newTexture.SetPixel(x, y, Color.white);
                }
                else
                {
                    newTexture.SetPixel(x, y, Color.black);
                }
            }
        }

        // Apply the changes to the texture
        newTexture.Apply();
        GetComponent<Renderer>().material.mainTexture = newTexture;
    }

    void drawGrayscaleCircle()
    {
        Renderer renderer = GetComponent<Renderer>();
        Texture2D texture = renderer.material.mainTexture as Texture2D;
        if (texture == null)
        {
            Debug.LogError("No texture found on the material.");
            return;
        }

        // Create a new texture based on the existing one
        Texture2D newTexture = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);

        // Calculate center of texture
        Vector2 center = new Vector2(texture.width / 2, texture.height / 2);
        float maxDistance = radius; // Use the existing radius variable

        // Fill the texture with grayscale values based on distance
        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float normalizedDistance = Mathf.Clamp01(distance / maxDistance);
                Color pixelColor = new Color(normalizedDistance, normalizedDistance, normalizedDistance);
                newTexture.SetPixel(x, y, pixelColor);
            }
        }

        newTexture.Apply();
        renderer.material.mainTexture = newTexture;
    }

    void draw3DSphereSlice(float zCoord)
    {
        Renderer renderer = GetComponent<Renderer>();
        Texture2D texture = renderer.material.mainTexture as Texture2D;
        if (texture == null)
        {
            Debug.LogError("No texture found on the material.");
            return;
        }

        // Create a new texture based on the existing one
        Texture2D newTexture = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);

        // Calculate center of texture
        Vector2 center = new Vector2(texture.width / 2, texture.height / 2);
        float maxRadius = radius; // Maximum radius from slider2

        // Normalize z-coordinate from [-2.5, 2.5] to [-1, 1] for sphere calculations
        float normalizedZ = zCoord / 2.5f;

        // Calculate the radius of the slice at this z position
        float sliceRadius = maxRadius * Mathf.Sqrt(Mathf.Max(0, 1 - normalizedZ * normalizedZ));

        // Fill the texture with the slice visualization
        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
            {
                // Calculate distance in pixel space
                float dx = (x - center.x);
                float dy = (y - center.y);
                float pixelDistance = Mathf.Sqrt(dx * dx + dy * dy);

                // Determine pixel color - solid white for the slice intersection
                Color pixelColor;
                if (pixelDistance <= sliceRadius && Mathf.Abs(normalizedZ) <= 1.0f)
                {
                    pixelColor = Color.white;
                }
                else
                {
                    pixelColor = Color.black;
                }

                newTexture.SetPixel(x, y, pixelColor);
            }
        }

        newTexture.Apply();
        renderer.material.mainTexture = newTexture;

        // Update the mesh for visualization
        meshScript mscript = GameObject.Find("GameObjectMesh").GetComponent<meshScript>();
        List<Vector3> vertices = new List<Vector3>();
        List<int> indices = new List<int>();

        // Scale quad to match slice radius and convert z to [-0.5, 0.5] range for visualization
        float quadScale = sliceRadius / (float)maxRadius;
        float visualZ = normalizedZ * 0.5f; // Scale to [-0.5, 0.5] for visualization

        // Only show quad when it intersects the sphere
        if (Mathf.Abs(normalizedZ) <= 1.0f)
        {
            vertices.Add(new Vector3(-0.5f * quadScale, -0.5f * quadScale, visualZ));
            vertices.Add(new Vector3(0.5f * quadScale, -0.5f * quadScale, visualZ));
            vertices.Add(new Vector3(0.5f * quadScale, 0.5f * quadScale, visualZ));
            vertices.Add(new Vector3(-0.5f * quadScale, 0.5f * quadScale, visualZ));

            // Define quad edges
            indices.Add(0); indices.Add(1);
            indices.Add(1); indices.Add(2);
            indices.Add(2); indices.Add(3);
            indices.Add(3); indices.Add(0);
        }

        mscript.createMeshGeometry(vertices, indices);
    }







    /* SOMETHING ELSE */

    // Update is called once per frame
    void Update()
    {


    }

    ushort pixelval(Vector2 p, int xdim, ushort[] pixels)
    {
        return pixels[(int)p.x + (int)p.y * xdim];
    }

    void setTexture(Slice slice)
    {
        int xdim = slice.sliceInfo.Rows;
        int ydim = slice.sliceInfo.Columns;

        var texture = new Texture2D(xdim, ydim, TextureFormat.RGB24, false); // garbage collector will tackle that it is new'ed 

        ushort[] pixels = slice.getPixels();

        for (int y = 0; y < ydim; y++)
            for (int x = 0; x < xdim; x++)
            {
                float val = pixelval(new Vector2(x, y), xdim, pixels);
                //float v = (val - _minIntensity) / _maxIntensity; // maps [_minIntensity,_maxIntensity] to [0,1] , i.e.  _minIntensity to black and _maxIntensity to white
                float v = Mathf.Clamp01((val - _minIntensity) / Mathf.Max(_maxIntensity - _minIntensity, 1));
                texture.SetPixel(x, y, new UnityEngine.Color(v, v, v));
            }

        texture.filterMode = FilterMode.Point;  // nearest neigbor interpolation is used.  (alternative is FilterMode.Bilinear)
        texture.Apply();  // Apply all SetPixel calls
        GetComponent<Renderer>().material.mainTexture = texture;
    }

    Slice[] processSlices(string dicomfilepath)
    {
        string[] dicomfilenames = Directory.GetFiles(dicomfilepath, "*.IMA");
        _numSlices = dicomfilenames.Length;

        Slice[] slices = new Slice[_numSlices];

        float max = -1;
        float min = 99999;
        for (int i = 0; i < _numSlices; i++)
        {
            string filename = dicomfilenames[i];
            slices[i] = new Slice(filename);
            SliceInfo info = slices[i].sliceInfo;
            if (info.LargestImagePixelValue > max) max = info.LargestImagePixelValue;
            if (info.SmallestImagePixelValue < min) min = info.SmallestImagePixelValue;
            // divide data by max before inserting into textre
            // alternatively divide 2^dicombitdepth, but that would become 4096 in this instance

        }
        print("Number of slices read:" + _numSlices);
        print("Max intensity in all slices:" + max);
        print("Min intensity in all slices:" + min);

        _minIntensity = (int)min;
        _maxIntensity = (int)max;

        Array.Sort(slices);

        return slices;
    }

    void drawSphereSlice(float zCoord)
    {
        Debug.Log($"Imagine that the sphere was just sliced at: {zCoord}");

    }






    /* BUTTONS */

    // Slider callbacks
    public void slice1SliderChange(ChangeEvent<float> evt)
    {
        Debug.Log($"IsoValue: {evt.newValue}");
    }

    public void slice2SliderChange(ChangeEvent<float> evt)
    {
        Debug.Log($"Radius: {evt.newValue}");
        // Update radius for 2D visualizations only
        radius = Mathf.RoundToInt(evt.newValue);

        if (circleType == 1)
            drawCircle1();
        else if (circleType == 2)
            drawCircle2();
        else if (circleType == 3)
            draw3DSphereSlice(_slider3.value);
    }

    public void slice3SliderChange(ChangeEvent<float> evt)
    {
        Debug.Log($"Z-Axis: {evt.newValue}");
        // Activate 3D mode when slider3 changes
        circleType = 3;
        draw3DSphereSlice(evt.newValue);
    }

    public void slice4SliderChange(ChangeEvent<float> evt)
    {
        Debug.Log($"Resolution: {Mathf.RoundToInt(evt.newValue)}");
    }

    // Button callbacks
    public void button1Pushed(ClickEvent evt)
    {
        GenerateMarchingSquares(_slider1.value);
    }

    public void button2Pushed(ClickEvent evt)
    {
        circleType = 1;
        drawCircle1();
    }

    public void button3Pushed(ClickEvent evt)
    {
        circleType = 2;
        drawCircle2();
    }

    public void button4Pushed(ClickEvent evt)
    {
        meshScript mscript = GameObject.Find("GameObjectMesh").GetComponent<meshScript>();
        if (mscript != null)
        {
            doMarching();
        }
    }

    public void button5Pushed(ClickEvent evt)
    {
        meshScript mscript = GameObject.Find("GameObjectMesh").GetComponent<meshScript>();
        if (mscript != null)
        {
            if (_toggle1.value == false) // Test mode
            {
                // Process a section from the middle where we expect to see features
                int middleIndex = _slices.Length / 2;
                ProcessSliceSubset(_slices, middleIndex + 5, 10); // Process 20 slices from middle
            }
            else // Full processing mode
            {
                Debug.Log("Starting full CT scan processing...");
                ProcessWithTiming(_slices);
            }
        }
        else
        {
            Debug.LogError("Mesh script not found on GameObjectMesh.");
        }
    }








    /* MARCHING TETRAHEDRONS */

    private void doMarching()
    {

        _vertices.Clear(); 
        _indices.Clear(); 
        _index = 0;

        float radius = _slider2.value;
        float isoLevel = _slider1.value;
        int resolution = (int)_slider4.value;

        var stepSize = (2.0f * radius) / resolution;

        for (int x = 0; x < resolution; x++)
        {
            for (int y = 0; y < resolution; y++)
            {
                for (int z = 0; z < resolution; z++)
                {
                    var cubeOrigin = new Vector3(
                        -radius + x * stepSize, 
                        -radius + y * stepSize, 
                        -radius + z * stepSize
                    );
                    var p100 = cubeOrigin + new Vector3(stepSize, 0, 0); 
                    var p010 = cubeOrigin + new Vector3(0, stepSize, 0); 
                    var p110 = cubeOrigin + new Vector3(stepSize, stepSize, 0); 
                    var p001 = cubeOrigin + new Vector3(0, 0, stepSize); 
                    var p101 = cubeOrigin + new Vector3(stepSize, 0, stepSize); 
                    var p011 = cubeOrigin + new Vector3(0, stepSize, stepSize); 
                    var p111 = cubeOrigin + new Vector3(stepSize, stepSize, stepSize);
                    var v000 = sphereFunction(cubeOrigin); 
                    var v100 = sphereFunction(p100); 
                    var v010 = sphereFunction(p010); 
                    var v110 = sphereFunction(p110); 
                    var v001 = sphereFunction(p001); 
                    var v101 = sphereFunction(p101); 
                    var v011 = sphereFunction(p011); 
                    var v111 = sphereFunction(p111);
                    doTetra(isoLevel * 0.1f, cubeOrigin, p100, p110, p111, v000, v100, v110, v111); 
                    doTetra(isoLevel * 0.1f, cubeOrigin, p110, p010, p111, v000, v110, v010, v111); 
                    doTetra(isoLevel * 0.1f, cubeOrigin, p010, p011, p111, v000, v010, v011, v111); 
                    doTetra(isoLevel * 0.1f, cubeOrigin, p011, p001, p111, v000, v011, v001, v111); 
                    doTetra(isoLevel * 0.1f, cubeOrigin, p001, p101, p111, v000, v001, v101, v111); 
                    doTetra(isoLevel * 0.1f, cubeOrigin, p101, p100, p111, v000, v101, v100, v111); 
                }
            }
        }

        meshScript mscript = GameObject.Find("GameObjectMesh").GetComponent<meshScript>();
        mscript.createMeshGeometry(_vertices, _indices, true);
        mscript.MeshToFile1(Application.dataPath + "/mesh.obj", ref _vertices, ref _indices);
    }

    private float sphereFunction(Vector3 point)
    {
        float radius = _slider2.value;
        return Mathf.Sqrt(Mathf.Pow(point.x, 2) + Mathf.Pow(point.y, 2) + Mathf.Pow(point.z, 2)) / (radius * 10);
    }

    //pseudocode "doCube()"
    private void doTetra(float iso, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 p4, float v1, float v2, float v3, float v4)
    { 
        var b1 = v1 >= iso;
        var b2 = v2 >= iso;
        var b3 = v3 >= iso;
        var b4 = v4 >= iso;
        var p12 = interpolateVertex(p1, p2, v1, v2, iso);
        var p13 = interpolateVertex(p1, p3, v1, v3, iso);
        var p14 = interpolateVertex(p1, p4, v1, v4, iso);
        var p23 = interpolateVertex(p2, p3, v2, v3, iso);
        var p24 = interpolateVertex(p2, p4, v2, v4, iso);
        var p34 = interpolateVertex(p3, p4, v3, v4, iso);
        var pattern = (b1 ? "1" : "0") + (b2 ? "1" : "0") + (b3 ? "1" : "0") + (b4 ? "1" : "0");
        switch (pattern)
        { 
            case "0001": addTriangle(p14, p24, p34); break;
            case "1110": addTriangle(p14, p34, p24); break;
            case "0010": addTriangle(p13, p34, p23); break;
            case "1101": addTriangle(p13, p23, p34); break;
            case "0100": addTriangle(p12, p23, p24); break;
            case "1011": addTriangle(p12, p24, p23); break;
            case "0111": addTriangle(p12, p13, p14); break;
            case "1000": addTriangle(p12, p14, p13); break;
            case "0011": addQuad(p13, p14, p24, p23); break;
            case "1100": addQuad(p13, p23, p24, p14); break;
            case "0101": addQuad(p12, p23, p34, p14); break;
            case "1010": addQuad(p12, p14, p34, p23); break;
            case "0110": addQuad(p12, p13, p34, p24); break;
            case "1001": addQuad(p12, p24, p34, p13); break;
        } 
    }

        private void addTriangle(Vector3 p1, Vector3 p2, Vector3 p3)
    { 
        _vertices.Add(p1);
        _vertices.Add(p2);
        _vertices.Add(p3);
        _indices.Add(_index++);
        _indices.Add(_index++);
        _indices.Add(_index++);
    }
    private void addQuad(Vector3 p1, Vector3 p2, Vector3 p3, Vector3 p4)
    { 
        addTriangle(p1, p2, p3);
        addTriangle(p1, p3, p4);
    }

    private static Vector3 interpolateVertex(Vector3 p1, Vector3 p2, float v1, float v2, float iso)
    { 
        var t = (iso - v1) / (v2 - v1);
        return p1 + (p2 - p1) * t;
    }








    /* CT SCAN (TASK 1c) */

    float[,,] ConvertSlicesToVoxelData(Slice[] _slices)
    {
        int width = _slices[0].sliceInfo.Rows;
        int height = _slices[0].sliceInfo.Columns;
        int depth = _slices.Length;

        float[,,] voxelData = new float[width, height, depth];

        // Use the min/max values already calculated during slice processing
        float minVal = _minIntensity;
        float maxVal = _maxIntensity;

        Debug.Log($"Using intensity range: {minVal} to {maxVal}");

        for (int z = 0; z < depth; z++)
        {
            ushort[] pixels = _slices[z].getPixels();
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    float value = pixels[x + y * width];
                    voxelData[x, y, z] = (value - minVal) / (maxVal - minVal);
                }
            }
        }

        return voxelData;
    }

    private float SamplePointCT(Vector3 point, float[,,] voxelData)
    {
        int x = Mathf.RoundToInt(point.x);
        int y = Mathf.RoundToInt(point.y);
        int z = Mathf.RoundToInt(point.z);

        if (x < 0 || x >= voxelData.GetLength(0) || 
            y < 0 || y >= voxelData.GetLength(1) || 
            z < 0 || z >= voxelData.GetLength(2))
            return 0f;

        return voxelData[x, y, z];
    }

    private void GenerateHeadMesh(List<Vector3> vertices, List<int> triangles, float[,,] voxelData)
    {

        _vertices.Clear();
        _indices.Clear();
        _index = 0;

        float isoLevel = _slider1.value;

        // Get pixel spacing and slice spacing from DICOM info
        float pixelSpacingX = _slices[0].sliceInfo.PixelSpacing[0];
        float pixelSpacingY = _slices[0].sliceInfo.PixelSpacing[1];
        float sliceSpacing = Mathf.Abs(_slices[1].sliceInfo.SliceLocation - _slices[0].sliceInfo.SliceLocation);

        // Create scale vector using actual spacings
        Vector3 scale = new Vector3(
            pixelSpacingX,
            pixelSpacingY,
            sliceSpacing
        );

        Debug.Log($"Using spacing: X={pixelSpacingX}mm, Y={pixelSpacingY}mm, Z={sliceSpacing}mm");

        Vector3 offset = new Vector3(
            -voxelData.GetLength(0) / 2f,
            -voxelData.GetLength(1) / 2f,
            -voxelData.GetLength(2) / 2f
        );

        int width = voxelData.GetLength(0);
        int height = voxelData.GetLength(1);
        int depth = voxelData.GetLength(2);

        Debug.Log($"Processing volume of size: {width}x{height}x{depth}");
        Debug.Log($"Using isoLevel: {isoLevel}");

        // Process each voxel cell using Marching Tetrahedrons
        for (int x = -10; x < width + 10; x++)
        {
            for (int y = -10; y < height + 10; y++)
            {
                for (int z = -10; z < depth + 10; z++)
                {
                    var v000 = SamplePointCT(new Vector3(x, y, z), voxelData);
                    var v100 = SamplePointCT(new Vector3(x + 1, y, z), voxelData);
                    var v010 = SamplePointCT(new Vector3(x, y + 1, z), voxelData);
                    var v110 = SamplePointCT(new Vector3(x + 1, y + 1, z), voxelData);
                    var v001 = SamplePointCT(new Vector3(x, y, z + 1), voxelData);
                    var v101 = SamplePointCT(new Vector3(x + 1, y, z + 1), voxelData);
                    var v011 = SamplePointCT(new Vector3(x, y + 1, z + 1), voxelData);
                    var v111 = SamplePointCT(new Vector3(x + 1, y + 1, z + 1), voxelData);
                    if (!(v000 > 0) && !(v100 > 0) && !(v010 > 0) && !(v110 > 0) &&
                        !(v001 > 0) && !(v101 > 0) && !(v011 > 0) && !(v111 > 0)) continue;
                    var p000 = Vector3.Scale(new Vector3(x, y, z) + offset, scale);
                    var p100 = Vector3.Scale(new Vector3(x + 1, y, z) + offset, scale);
                    var p010 = Vector3.Scale(new Vector3(x, y + 1, z) + offset, scale);
                    var p110 = Vector3.Scale(new Vector3(x + 1, y + 1, z) + offset, scale);
                    var p001 = Vector3.Scale(new Vector3(x, y, z + 1) + offset, scale);
                    var p101 = Vector3.Scale(new Vector3(x + 1, y, z + 1) + offset, scale);
                    var p011 = Vector3.Scale(new Vector3(x, y + 1, z + 1) + offset, scale);
                    var p111 = Vector3.Scale(new Vector3(x + 1, y + 1, z + 1) + offset, scale);
                    doTetra(isoLevel, p000, p100, p110, p111, v000, v100, v110, v111);
                    doTetra(isoLevel, p000, p110, p010, p111, v000, v110, v010, v111);
                    doTetra(isoLevel, p000, p010, p011, p111, v000, v010, v011, v111);
                    doTetra(isoLevel, p000, p011, p001, p111, v000, v011, v001, v111);
                    doTetra(isoLevel, p000, p001, p101, p111, v000, v001, v101, v111);
                    doTetra(isoLevel, p000, p101, p100, p111, v000, v101, v100, v111); 
                }
            }
        }
    }

    void ProcessAllSlices(Slice[] _slices)
    {
        _vertices.Clear();
        _indices.Clear();

        // Convert all slices to voxel data at once
        float[,,] voxelData = ConvertSlicesToVoxelData(_slices);

        GenerateHeadMesh(_vertices, _indices, voxelData);

        // Save to OBJ file
        string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string filename = $"CTScan_{timestamp}.obj";
        meshScript mscript = GameObject.Find("GameObjectMesh").GetComponent<meshScript>();
        mscript.createMeshGeometry(_vertices, _indices, true);
        mscript.MeshToFile1(filename, ref _vertices, ref _indices);

        Debug.Log($"Mesh generated with {_vertices.Count} vertices and {_indices.Count / 3} triangles");
        Debug.Log($"Saved to {filename}");
    }

    void ProcessSliceSubset(Slice[] _slices, int startIndex, int count)
    {
        Debug.Log($"Processing subset of {count} slices starting at index {startIndex}");
        Debug.Log($"Total available slices: {_slices.Length}");
        Debug.Log($"Slider1 (isoLevel) value: {_slider1.value}");

        // Take subset of slices
        Slice[] subset = _slices.Skip(startIndex).Take(count).ToArray();

        if (subset.Length == 0)
        {
            Debug.LogError("No slices in subset!");
            return;
        }

        // Process subset
        _vertices.Clear();
        _indices.Clear();

        float[,,] voxelData = ConvertSlicesToVoxelData(subset);
        GenerateHeadMesh(_vertices, _indices, voxelData);

        if (_vertices.Count == 0)
        {
            Debug.LogWarning("No vertices generated! Try adjusting the isoLevel.");
            return;
        }

        // Save subset to OBJ
        string filename = $"CTScan_subset_{startIndex}_{count}.obj";
        meshScript mscript = GameObject.Find("GameObjectMesh").GetComponent<meshScript>();
        mscript.createMeshGeometry(_vertices, _indices, true);
        mscript.MeshToFile1(filename, ref _vertices, ref _indices);

        Debug.Log($"Subset mesh generated with {_vertices.Count} vertices and {_indices.Count / 3} triangles");
    }

    void ProcessWithTiming(Slice[] _slices)
    {
        System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
        stopwatch.Start();

        ProcessAllSlices(_slices);

        stopwatch.Stop();
        Debug.Log($"Total processing time: {stopwatch.ElapsedMilliseconds / 1000f} seconds");
    }
}
using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class AutoSceneBuilder
{
    private const string FontsPath = "Assets/Fonts/Maplestory Bold.ttf";
    private const string FontAssetPath = "Assets/Fonts/Maplestory Bold SDF.asset";
    private const string FamilyTexturesDir = "Assets/Textures/Family";
    private const string BusFacesDir = "Assets/Textures/BusFaces";

    [MenuItem("CocoNara/1. 게임 씬 완전 자동 구축 (Build Complete Scene)", false, 1)]
    public static void BuildCompleteGameScene()
    {
        Debug.Log("<color=#00FF88>[CocoNara] 씬 자동 구축을 시작합니다...</color>");

        // 0. URP 핑크 머티리얼 일괄 변환 및 해결
        ConvertAllLegacyMaterialsToURP();

        // 1. 필요한 텍스처 및 폰트 에셋 준비
        EnsureDirectories();
        GenerateBusFaceTextures();
        EnsureFamilyMemberTextures();
        TMP_FontAsset fontAsset = GetOrCreateTMPFontAsset();

        // 2. 씬 기본 환경 초기화
        GameObject worldRoot = GetOrCreateRoot("WorldRoot");
        SetupLightingAndSky();

        // 3. 도로 및 맵 지형 구축
        SetupRoadNetworkAndGround(worldRoot);

        // 4. 12개 주요 랜드마크 구축
        Dictionary<string, Landmark> landmarks = SetupAllLandmarks(worldRoot, fontAsset);

        // 5. 꼬마버스 생성 및 세팅 (아이파크캐슬 출발점)
        BusController bus = SetupBus(worldRoot, landmarks["아이파크캐슬"].transform.position + Vector3.forward * 4f);

        // 6. 메인 카메라 세팅
        SetupCamera(bus.transform);

        // 7. 가족 승객 2.5D 빌보드 생성 및 거점 배치
        SetupFamilyPassengers(worldRoot, landmarks, fontAsset);

        // 8. 모바일 조작 및 미니게임 UI 캔버스 구축
        SetupUI(bus, fontAsset);

        // 9. 매니저 오브젝트 세팅
        SetupManagers(worldRoot);

        // 10. New Input System 모듈 호환성 검증 및 교체
        FixEventSystemInputModule();

        // 씬 저장
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log("<color=#00FF88>[CocoNara] 🎉 게임 씬이 성공적으로 자동 구축되었습니다! Play 버튼을 눌러 테스트해보세요.</color>");
        if (!Application.isBatchMode)
        {
            EditorUtility.DisplayDialog("CocoNaraBusStory", "🎉 코코나라 꼬마버스 게임 씬이 성공적으로 구축되었습니다!\n\n1. 핑크색 머티리얼이 URP Lit 셰이더로 일괄 변환되었습니다.\n2. EventSystem이 New Input System 전용 모듈로 자동 교체되었습니다.\n3. 12개 랜드마크, 가족 승객, 2.5D 표정 버스, 모바일 온스크린 컨트롤러, 미니게임 팝업이 모두 연결되었습니다.", "확인");
        }
    }

    [MenuItem("CocoNara/2. 핑크색 머티리얼 URP로 일괄 변환 (Fix Pink Materials)", false, 2)]
    public static void ConvertAllLegacyMaterialsToURP()
    {
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null)
        {
            Debug.LogWarning("[CocoNara] Universal Render Pipeline/Lit 셰이더를 찾을 수 없습니다.");
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:Material", new[] { "Assets" });
        int converted = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null || mat.shader == null) continue;

            string sName = mat.shader.name;
            bool isLegacy = sName == "Standard" ||
                            sName.StartsWith("Standard (") ||
                            sName.StartsWith("Legacy Shaders/") ||
                            sName.StartsWith("Mobile/") ||
                            sName.StartsWith("Unlit/") ||
                            sName == "Hidden/InternalErrorShader" ||
                            sName.Contains("Error");

            if (isLegacy)
            {
                Color mainColor = Color.white;
                if (mat.HasProperty("_Color")) mainColor = mat.GetColor("_Color");
                else if (mat.HasProperty("_BaseColor")) mainColor = mat.GetColor("_BaseColor");

                Texture mainTex = null;
                if (mat.HasProperty("_MainTex")) mainTex = mat.GetTexture("_MainTex");
                else if (mat.HasProperty("_BaseMap")) mainTex = mat.GetTexture("_BaseMap");

                mat.shader = urpLit;

                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", mainColor);
                if (mainTex != null && mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", mainTex);

                EditorUtility.SetDirty(mat);
                converted++;
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"<color=#00FF88>[CocoNara] {converted}개의 레거시 머티리얼을 URP Lit 셰이더로 성공적으로 변환했습니다!</color>");
        if (!Application.isBatchMode && converted > 0)
        {
            EditorUtility.DisplayDialog("CocoNaraBusStory", $"{converted}개의 구버전 핑크색 머티리얼을 URP 전용 Lit 셰이더로 정상 변환했습니다!", "확인");
        }
    }

    [MenuItem("CocoNara/3. EventSystem 입력 모듈 오류 수정 (Fix Input Error)", false, 3)]
    public static void FixEventSystemInputModule()
    {
        EventSystem[] allES = UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None);
        int fixedCount = 0;

        Type newModuleType = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");

        foreach (var es in allES)
        {
            // 구버전 StandaloneInputModule 제거
            StandaloneInputModule sim = es.GetComponent<StandaloneInputModule>();
            if (sim != null)
            {
                UnityEngine.Object.DestroyImmediate(sim);
                fixedCount++;
            }

            // 신규 InputSystemUIInputModule 추가
            if (newModuleType != null && es.GetComponent(newModuleType) == null)
            {
                es.gameObject.AddComponent(newModuleType);
                fixedCount++;
            }

            EditorUtility.SetDirty(es.gameObject);
        }

        if (fixedCount > 0)
        {
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("<color=#00FF88>[CocoNara] EventSystem이 New Input System 전용 InputSystemUIInputModule로 교체되었습니다!</color>");
        }
    }

    [MenuItem("CocoNara/4. 버스 표정 및 에셋 재생성 (Generate Textures)", false, 4)]
    public static void GenerateAssetsOnly()
    {
        EnsureDirectories();
        GenerateBusFaceTextures();
        EnsureFamilyMemberTextures();
        GetOrCreateTMPFontAsset();
        AssetDatabase.Refresh();
        if (!Application.isBatchMode)
        {
            EditorUtility.DisplayDialog("CocoNaraBusStory", "표정 텍스처 및 에셋이 정상 생성되었습니다.", "확인");
        }
    }

    private static void EnsureDirectories()
    {
        if (!Directory.Exists(BusFacesDir)) Directory.CreateDirectory(BusFacesDir);
        if (!Directory.Exists(FamilyTexturesDir)) Directory.CreateDirectory(FamilyTexturesDir);
        AssetDatabase.Refresh();
    }

    #region 텍스처 & 폰트 생성

    private static void GenerateBusFaceTextures()
    {
        string normalPath = Path.Combine(BusFacesDir, "BusFace_Normal.png");
        string winkPath = Path.Combine(BusFacesDir, "BusFace_Wink.png");
        string chargingPath = Path.Combine(BusFacesDir, "BusFace_Charging.png");

        if (!File.Exists(normalPath))
        {
            Texture2D tex = CreateProceduralFaceTexture(FaceType.Normal);
            File.WriteAllBytes(normalPath, tex.EncodeToPNG());
            AssetDatabase.ImportAsset(normalPath);
        }

        if (!File.Exists(winkPath))
        {
            Texture2D tex = CreateProceduralFaceTexture(FaceType.Wink);
            File.WriteAllBytes(winkPath, tex.EncodeToPNG());
            AssetDatabase.ImportAsset(winkPath);
        }

        if (!File.Exists(chargingPath))
        {
            Texture2D tex = CreateProceduralFaceTexture(FaceType.Charging);
            File.WriteAllBytes(chargingPath, tex.EncodeToPNG());
            AssetDatabase.ImportAsset(chargingPath);
        }
    }

    private enum FaceType { Normal, Wink, Charging }

    private static Texture2D CreateProceduralFaceTexture(FaceType type)
    {
        int size = 512;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color clear = new Color(0, 0, 0, 0);

        // 투명 배경 초기화
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                tex.SetPixel(x, y, clear);

        // 눈 위치 세팅
        Vector2 leftEye = new Vector2(170, 310);
        Vector2 rightEye = new Vector2(342, 310);
        int eyeRadius = 55;

        Color eyeColor = new Color(0.12f, 0.15f, 0.22f);
        Color pupilColor = new Color(0.2f, 0.45f, 0.85f);
        Color white = Color.white;
        Color pink = new Color(1f, 0.5f, 0.65f, 0.7f);

        // 빰 볼터치
        DrawFilledCircle(tex, new Vector2(120, 230), 40, pink);
        DrawFilledCircle(tex, new Vector2(392, 230), 40, pink);

        if (type == FaceType.Normal)
        {
            // 큰 똘망똘망한 눈
            DrawFilledCircle(tex, leftEye, eyeRadius, eyeColor);
            DrawFilledCircle(tex, rightEye, eyeRadius, eyeColor);
            DrawFilledCircle(tex, leftEye + new Vector2(0, 10), eyeRadius - 15, pupilColor);
            DrawFilledCircle(tex, rightEye + new Vector2(0, 10), eyeRadius - 15, pupilColor);
            // 반짝이는 하이라이트
            DrawFilledCircle(tex, leftEye + new Vector2(-15, 18), 16, white);
            DrawFilledCircle(tex, leftEye + new Vector2(16, -10), 9, white);
            DrawFilledCircle(tex, rightEye + new Vector2(-15, 18), 16, white);
            DrawFilledCircle(tex, rightEye + new Vector2(16, -10), 9, white);
        }
        else if (type == FaceType.Wink)
        {
            // 왼쪽 윙크 (^_~)
            DrawWinkEye(tex, leftEye, eyeRadius);
            // 오른쪽 똘망눈
            DrawFilledCircle(tex, rightEye, eyeRadius, eyeColor);
            DrawFilledCircle(tex, rightEye + new Vector2(0, 10), eyeRadius - 15, pupilColor);
            DrawFilledCircle(tex, rightEye + new Vector2(-15, 18), 18, white);
            DrawFilledCircle(tex, rightEye + new Vector2(16, -10), 10, white);
        }
        else if (type == FaceType.Charging)
        {
            // 별빛 눈 (*_*)
            DrawStarEye(tex, leftEye, 50, new Color(1f, 0.9f, 0.2f));
            DrawStarEye(tex, rightEye, 50, new Color(1f, 0.9f, 0.2f));
        }

        // 활짝 웃는 입 (Curved Smile)
        DrawSmileMouth(tex, new Vector2(256, 170), 95, 45, type == FaceType.Wink);

        tex.Apply();
        return tex;
    }

    private static void DrawFilledCircle(Texture2D tex, Vector2 center, int radius, Color color)
    {
        int r2 = radius * radius;
        for (int y = -radius; y <= radius; y++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                if (x * x + y * y <= r2)
                {
                    int px = (int)center.x + x;
                    int py = (int)center.y + y;
                    if (px >= 0 && px < tex.width && py >= 0 && py < tex.height)
                    {
                        Color existing = tex.GetPixel(px, py);
                        tex.SetPixel(px, py, Color.Lerp(existing, color, color.a));
                    }
                }
            }
        }
    }

    private static void DrawWinkEye(Texture2D tex, Vector2 center, int radius)
    {
        for (int x = -radius; x <= radius; x++)
        {
            float normX = (float)x / radius;
            float yArc = (1f - normX * normX) * (radius * 0.7f);
            int py = (int)(center.y + yArc - 15);
            int px = (int)center.x + x;
            DrawThickPoint(tex, px, py, 10, new Color(0.12f, 0.15f, 0.22f));
        }
    }

    private static void DrawStarEye(Texture2D tex, Vector2 center, int size, Color color)
    {
        DrawFilledCircle(tex, center, size / 2, color);
        for (int i = -size; i <= size; i++)
        {
            DrawThickPoint(tex, (int)center.x + i, (int)center.y, 10, color);
            DrawThickPoint(tex, (int)center.x, (int)center.y + i, 10, color);
            DrawThickPoint(tex, (int)center.x + i, (int)center.y + i, 8, color);
            DrawThickPoint(tex, (int)center.x + i, (int)center.y - i, 8, color);
        }
    }

    private static void DrawSmileMouth(Texture2D tex, Vector2 center, int width, int depth, bool openMouth)
    {
        Color lipColor = new Color(0.12f, 0.15f, 0.22f);
        Color mouthInside = new Color(0.9f, 0.25f, 0.35f);

        for (int x = -width; x <= width; x++)
        {
            float normX = (float)x / width;
            float bottomY = center.y - (1f - normX * normX) * depth;
            int px = (int)center.x + x;

            if (openMouth)
            {
                for (int y = (int)bottomY; y <= center.y; y++)
                {
                    if (px >= 0 && px < tex.width && y >= 0 && y < tex.height)
                    {
                        tex.SetPixel(px, y, mouthInside);
                    }
                }
            }

            DrawThickPoint(tex, px, (int)bottomY, 9, lipColor);
        }
    }

    private static void DrawThickPoint(Texture2D tex, int px, int py, int thickness, Color color)
    {
        int r = thickness / 2;
        for (int y = -r; y <= r; y++)
        {
            for (int x = -r; x <= r; x++)
            {
                int ix = px + x;
                int iy = py + y;
                if (ix >= 0 && ix < tex.width && iy >= 0 && iy < tex.height)
                {
                    tex.SetPixel(ix, iy, color);
                }
            }
        }
    }

    private static void EnsureFamilyMemberTextures()
    {
        // 선율, 태율, 엄마, 아빠 외에 친할머니, 친할아버지, 외할머니, 외할아버지 텍스처가 없을 경우 예쁜 파스텔톤 뱃지 자동 생성
        string[] extraMembers = { "친할머니", "친할아버지", "외할머니", "외할아버지" };
        Color[] badgeColors = {
            new Color(1f, 0.7f, 0.8f),
            new Color(0.6f, 0.8f, 1f),
            new Color(1f, 0.85f, 0.6f),
            new Color(0.7f, 0.9f, 0.7f)
        };

        for (int i = 0; i < extraMembers.Length; i++)
        {
            string filename = extraMembers[i] + ".png";
            string path = Path.Combine(FamilyTexturesDir, filename);
            if (!File.Exists(path))
            {
                Texture2D badge = CreateAvatarBadgeTexture(extraMembers[i], badgeColors[i]);
                File.WriteAllBytes(path, badge.EncodeToPNG());
                AssetDatabase.ImportAsset(path);
            }
        }
    }

    private static Texture2D CreateAvatarBadgeTexture(string name, Color bgColor)
    {
        int size = 256;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Vector2 center = new Vector2(size / 2, size / 2);
        int radius = size / 2 - 10;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                if (dist <= radius - 8)
                {
                    tex.SetPixel(x, y, bgColor);
                }
                else if (dist <= radius)
                {
                    tex.SetPixel(x, y, Color.white);
                }
                else
                {
                    tex.SetPixel(x, y, new Color(0, 0, 0, 0));
                }
            }
        }

        // 중앙에 미소 짓는 얼굴 심볼
        DrawFilledCircle(tex, center + new Vector2(-40, 25), 14, Color.black);
        DrawFilledCircle(tex, center + new Vector2(40, 25), 14, Color.black);
        DrawSmileMouth(tex, center + new Vector2(0, -25), 50, 25, true);

        tex.Apply();
        return tex;
    }

    private static TMP_FontAsset GetOrCreateTMPFontAsset()
    {
        TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        if (fontAsset != null) return fontAsset;

        Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(FontsPath);
        if (sourceFont != null)
        {
            try
            {
                fontAsset = TMP_FontAsset.CreateFontAsset(sourceFont);
                if (fontAsset != null)
                {
                    AssetDatabase.CreateAsset(fontAsset, FontAssetPath);
                    EditorUtility.SetDirty(fontAsset);
                    AssetDatabase.SaveAssets();
                    return fontAsset;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[CocoNara] TMP_FontAsset 자동 생성 실패 (기본 TMP 폰트로 대체): " + e.Message);
            }
        }

        return TMP_Settings.defaultFontAsset;
    }

    #endregion

    #region 씬 및 맵 구축

    private static GameObject GetOrCreateRoot(string name)
    {
        GameObject obj = GameObject.Find(name);
        if (obj == null)
        {
            obj = new GameObject(name);
        }
        return obj;
    }

    private static void SetupLightingAndSky()
    {
        Light mainLight = null;
        Light[] lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
        foreach (var l in lights)
        {
            if (l.type == LightType.Directional)
            {
                mainLight = l;
                break;
            }
        }

        if (mainLight == null)
        {
            GameObject lightObj = new GameObject("Directional Light");
            mainLight = lightObj.AddComponent<Light>();
            mainLight.type = LightType.Directional;
        }

        mainLight.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
        mainLight.color = new Color(1f, 0.96f, 0.88f);
        mainLight.intensity = 1.35f;
        mainLight.shadows = LightShadows.Soft;

        RenderSettings.ambientLight = new Color(0.6f, 0.65f, 0.75f);
    }

    private static void SetupRoadNetworkAndGround(GameObject parent)
    {
        GameObject mapRoot = new GameObject("MapEnvironment");
        mapRoot.transform.SetParent(parent.transform);

        // 1. 대형 잔디 바닥 생성 (280m x 280m)
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground_Grass";
        ground.transform.SetParent(mapRoot.transform);
        ground.transform.position = Vector3.zero;
        ground.transform.localScale = new Vector3(30f, 1f, 30f);

        Material grassMat = GetOrCreateMaterial("Mat_CoCoGrass", new Color(0.38f, 0.72f, 0.35f));
        ground.GetComponent<MeshRenderer>().sharedMaterial = grassMat;

        // 2. 도로 네트워크 구축 (부드럽게 주행 가능한 십자형 & 순환 루프 도로)
        Material roadMat = GetOrCreateMaterial("Mat_CoCoRoad", new Color(0.24f, 0.25f, 0.28f));
        Material lineMat = GetOrCreateMaterial("Mat_CoCoRoadLine", new Color(1f, 0.88f, 0.2f));

        // 메인 대로 4개 (가로, 세로 도로)
        CreateRoadStrip(mapRoot.transform, new Vector3(0, 0.05f, 0), new Vector3(14f, 1f, 260f), roadMat, lineMat, true);
        CreateRoadStrip(mapRoot.transform, new Vector3(0, 0.05f, 0), new Vector3(260f, 1f, 14f), roadMat, lineMat, false);

        // 외곽 순환 도로 (북쪽, 남쪽, 동쪽, 서쪽 루프)
        CreateRoadStrip(mapRoot.transform, new Vector3(0, 0.05f, 80f), new Vector3(260f, 1f, 12f), roadMat, lineMat, false);
        CreateRoadStrip(mapRoot.transform, new Vector3(0, 0.05f, -80f), new Vector3(260f, 1f, 12f), roadMat, lineMat, false);
        CreateRoadStrip(mapRoot.transform, new Vector3(80f, 0.05f, 0), new Vector3(12f, 1f, 260f), roadMat, lineMat, true);
        CreateRoadStrip(mapRoot.transform, new Vector3(-80f, 0.05f, 0), new Vector3(12f, 1f, 260f), roadMat, lineMat, true);
    }

    private static void CreateRoadStrip(Transform parent, Vector3 pos, Vector3 size, Material roadMat, Material lineMat, bool isVertical)
    {
        GameObject road = GameObject.CreatePrimitive(PrimitiveType.Cube);
        road.name = isVertical ? "Road_Vertical" : "Road_Horizontal";
        road.transform.SetParent(parent);
        road.transform.position = pos;
        road.transform.localScale = size;
        road.GetComponent<MeshRenderer>().sharedMaterial = roadMat;

        // 중앙 황색 실선
        GameObject line = GameObject.CreatePrimitive(PrimitiveType.Cube);
        line.name = "RoadCenterLine";
        line.transform.SetParent(road.transform);
        line.transform.localPosition = new Vector3(0, 0.52f, 0);
        line.transform.localScale = isVertical ? new Vector3(0.04f, 0.1f, 1f) : new Vector3(1f, 0.1f, 0.04f);
        line.GetComponent<MeshRenderer>().sharedMaterial = lineMat;
        UnityEngine.Object.DestroyImmediate(line.GetComponent<Collider>());
    }

    private static Material GetOrCreateMaterial(string name, Color color)
    {
        string path = $"Assets/Materials/{name}.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            if (!Directory.Exists("Assets/Materials")) Directory.CreateDirectory("Assets/Materials");
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            mat = new Material(shader);
            mat.color = color;
            AssetDatabase.CreateAsset(mat, path);
        }
        else
        {
            mat.color = color;
        }
        return mat;
    }

    #endregion

    #region 12개 랜드마크 구축

    private static Dictionary<string, Landmark> SetupAllLandmarks(GameObject parent, TMP_FontAsset fontAsset)
    {
        GameObject landmarksRoot = new GameObject("Landmarks");
        landmarksRoot.transform.SetParent(parent.transform);

        Dictionary<string, Landmark> dict = new Dictionary<string, Landmark>();

        // 1. 아이파크캐슬 (우리집 & 시작점)
        dict["아이파크캐슬"] = CreateLandmarkObject(landmarksRoot.transform, "아이파크캐슬", "우리집 🏠", new Vector3(-12f, 0f, 10f), new Color(0.95f, 0.45f, 0.25f), fontAsset, false);

        // 2. 코코어린이집 (태율이 등원)
        dict["코코어린이집"] = CreateLandmarkObject(landmarksRoot.transform, "코코어린이집", "태율이 어린이집 🍼", new Vector3(65f, 0f, 65f), new Color(1f, 0.75f, 0.2f), fontAsset, false);

        // 3. 나라유치원 (선율이 등원)
        dict["나라유치원"] = CreateLandmarkObject(landmarksRoot.transform, "나라유치원", "선율이 유치원 🎒", new Vector3(-65f, 0f, 65f), new Color(0.4f, 0.75f, 1f), fontAsset, false);

        // 4. 글빛공원 (힐링 공원)
        dict["글빛공원"] = CreateLandmarkObject(landmarksRoot.transform, "글빛공원", "가족 힐링 공원 🌳", new Vector3(0f, 0f, 85f), new Color(0.35f, 0.85f, 0.4f), fontAsset, false);

        // 5. 하남 스타필드 (대형 쇼핑몰)
        dict["하남 스타필드"] = CreateLandmarkObject(landmarksRoot.transform, "하남 스타필드", "대형 쇼핑몰 🛍️", new Vector3(-95f, 0f, 0f), new Color(0.85f, 0.3f, 0.5f), fontAsset, false);

        // 6. 전기차 충전소 (EV Station)
        dict["전기차 충전소"] = CreateLandmarkObject(landmarksRoot.transform, "전기차 충전소", "친환경 급속 충전 ⚡", new Vector3(95f, 0f, 0f), new Color(0.2f, 0.85f, 0.85f), fontAsset, true);

        // 7. 왕십리 삼성아파트 (친할머니, 친할아버지 댁)
        dict["왕십리 삼성아파트"] = CreateLandmarkObject(landmarksRoot.transform, "왕십리 삼성아파트", "친할머니댁 👵👴", new Vector3(-65f, 0f, -65f), new Color(0.55f, 0.45f, 0.85f), fontAsset, false);

        // 8. 하남 풍산 아이파크 (외할머니, 외할아버지 댁)
        dict["하남 풍산 아이파크"] = CreateLandmarkObject(landmarksRoot.transform, "하남 풍산 아이파크", "외할머니댁 👵👴", new Vector3(65f, 0f, -65f), new Color(0.95f, 0.6f, 0.7f), fontAsset, false);

        // 9. 에버랜드 (환상의 나라)
        dict["에버랜드"] = CreateLandmarkObject(landmarksRoot.transform, "에버랜드", "환상의 놀이공원 🎡", new Vector3(-115f, 0f, 85f), new Color(0.98f, 0.35f, 0.25f), fontAsset, false);

        // 10. 롯데월드 (모험의 나라)
        dict["롯데월드"] = CreateLandmarkObject(landmarksRoot.transform, "롯데월드", "모험과 신비의 나라 🎠", new Vector3(115f, 0f, 85f), new Color(0.35f, 0.55f, 0.95f), fontAsset, false);

        // 11. 김포공항 (국내선 공항)
        dict["김포공항"] = CreateLandmarkObject(landmarksRoot.transform, "김포공항", "국내선 공항 ✈️", new Vector3(-115f, 0f, -85f), new Color(0.45f, 0.7f, 0.85f), fontAsset, false);

        // 12. 인천공항 (국제선 공항 & 비행기 연출)
        Landmark incheon = CreateLandmarkObject(landmarksRoot.transform, "인천공항", "국제선 공항 🛫", new Vector3(115f, 0f, -85f), new Color(0.25f, 0.6f, 0.95f), fontAsset, false);
        dict["인천공항"] = incheon;

        // 인천공항 상공 비행기 비행 루프 추가
        SetupAirportAirplane(incheon.transform);

        return dict;
    }

    private static Landmark CreateLandmarkObject(Transform parent, string title, string subtitle, Vector3 pos, Color themeColor, TMP_FontAsset fontAsset, bool isCharging)
    {
        GameObject lmObj = new GameObject($"Landmark_{title}");
        lmObj.transform.SetParent(parent);
        lmObj.transform.position = pos;

        Landmark landmark = lmObj.AddComponent<Landmark>();
        landmark.landmarkName = title;
        landmark.subtitle = subtitle;
        landmark.isEVChargingStation = isCharging;

        // 1. 감지 트리거 콜라이더
        SphereCollider trigger = lmObj.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = 8.5f;

        // 2. 랜드마크 건물 비주얼 스폰 (임포트된 건물 프리팹 탐색 및 폴백 큐브)
        GameObject buildingVisual = TrySpawnBuildingPrefab(title, lmObj.transform, themeColor);
        buildingVisual.transform.localPosition = Vector3.zero;

        // 3. 3D 간판 플로팅 UI
        GameObject signRoot = new GameObject("SignRoot");
        signRoot.transform.SetParent(lmObj.transform);
        signRoot.transform.localPosition = new Vector3(0, 7.5f, 0);
        landmark.signRoot = signRoot.transform;

        // 간판 배경 판
        GameObject signBoard = GameObject.CreatePrimitive(PrimitiveType.Cube);
        signBoard.name = "SignBoard";
        signBoard.transform.SetParent(signRoot.transform);
        signBoard.transform.localPosition = Vector3.zero;
        signBoard.transform.localScale = new Vector3(7.5f, 2.2f, 0.35f);
        signBoard.GetComponent<MeshRenderer>().sharedMaterial = GetOrCreateMaterial($"Mat_Sign_{title}", themeColor);
        UnityEngine.Object.DestroyImmediate(signBoard.GetComponent<Collider>());

        // 간판 타이틀 TMP
        GameObject titleObj = new GameObject("SignTitle");
        titleObj.transform.SetParent(signRoot.transform);
        titleObj.transform.localPosition = new Vector3(0, 0.35f, -0.2f);
        TextMeshPro titleTMP = titleObj.AddComponent<TextMeshPro>();
        titleTMP.text = title;
        titleTMP.fontSize = 7f;
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.color = Color.white;
        titleTMP.fontStyle = FontStyles.Bold;
        if (fontAsset != null) titleTMP.font = fontAsset;
        landmark.signTitleTMP = titleTMP;

        // 간판 서브타이틀 TMP
        GameObject subObj = new GameObject("SignSubtitle");
        subObj.transform.SetParent(signRoot.transform);
        subObj.transform.localPosition = new Vector3(0, -0.45f, -0.2f);
        TextMeshPro subTMP = subObj.AddComponent<TextMeshPro>();
        subTMP.text = subtitle;
        subTMP.fontSize = 4.5f;
        subTMP.alignment = TextAlignmentOptions.Center;
        subTMP.color = new Color(1f, 0.95f, 0.6f);
        if (fontAsset != null) subTMP.font = fontAsset;
        landmark.signSubtitleTMP = subTMP;

        // 4. 도착 축하 파티클
        GameObject particleObj = new GameObject("ArrivalCelebrationParticle");
        particleObj.transform.SetParent(lmObj.transform);
        particleObj.transform.localPosition = new Vector3(0, 1.5f, 0);
        ParticleSystem ps = particleObj.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.startLifetime = 1.6f;
        main.startSpeed = 7f;
        main.startSize = 0.5f;
        main.startColor = isCharging ? new Color(0.2f, 0.9f, 1f) : new Color(1f, 0.85f, 0.2f);
        var emission = ps.emission;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 35) });
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        landmark.arrivalParticle = ps;

        return landmark;
    }

    private static GameObject TrySpawnBuildingPrefab(string landmarkName, Transform parent, Color themeColor)
    {
        // 프로젝트 내 건물 프리팹 검색
        string[] searchPatterns = {
            "Assets/SimplePoly City - Low Poly Assets/Prefab/Buildings/*.prefab",
            "Assets/ithappy/Cartoon_City_Free/Prefabs/Buildings/*.prefab"
        };

        foreach (var pattern in searchPatterns)
        {
            string dir = Path.GetDirectoryName(pattern);
            if (Directory.Exists(dir))
            {
                string[] files = Directory.GetFiles(dir, "*.prefab");
                if (files.Length > 0)
                {
                    // 일관된 시드로 랜드마크마다 다른 멋진 건물 배치
                    int index = Mathf.Abs(landmarkName.GetHashCode()) % files.Length;
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(files[index]);
                    if (prefab != null)
                    {
                        GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                        inst.transform.localScale = Vector3.one * 1.5f;
                        return inst;
                    }
                }
            }
        }

        // 폴백: 세련된 파스텔 큐브 건물
        GameObject fallback = GameObject.CreatePrimitive(PrimitiveType.Cube);
        fallback.transform.SetParent(parent);
        fallback.transform.localScale = new Vector3(10f, 12f, 10f);
        fallback.transform.localPosition = new Vector3(0, 6f, 0);
        fallback.GetComponent<MeshRenderer>().sharedMaterial = GetOrCreateMaterial($"Mat_Bldg_{landmarkName}", themeColor);
        return fallback;
    }

    private static void SetupAirportAirplane(Transform airportTransform)
    {
        GameObject airportAir = new GameObject("AirportAirplaneFlight");
        airportAir.transform.SetParent(airportTransform);
        airportAir.transform.localPosition = Vector3.zero;

        AirportPlaneAnimation anim = airportAir.AddComponent<AirportPlaneAnimation>();

        // 비행기 모델 생성 (단순 날개형 비행기)
        GameObject planeObj = new GameObject("AirplaneModel");
        planeObj.transform.SetParent(airportAir.transform);
        planeObj.transform.localPosition = new Vector3(0, 15f, 0);

        // 동체
        GameObject fuselage = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        fuselage.transform.SetParent(planeObj.transform);
        fuselage.transform.localScale = new Vector3(1.4f, 4f, 1.4f);
        fuselage.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        fuselage.GetComponent<MeshRenderer>().sharedMaterial = GetOrCreateMaterial("Mat_AirplaneBody", Color.white);
        UnityEngine.Object.DestroyImmediate(fuselage.GetComponent<Collider>());

        // 날개
        GameObject wing = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wing.transform.SetParent(planeObj.transform);
        wing.transform.localScale = new Vector3(8.5f, 0.2f, 1.8f);
        wing.transform.localPosition = new Vector3(0, 0.1f, 0.4f);
        wing.GetComponent<MeshRenderer>().sharedMaterial = GetOrCreateMaterial("Mat_AirplaneWing", new Color(0.2f, 0.5f, 0.95f));
        UnityEngine.Object.DestroyImmediate(wing.GetComponent<Collider>());

        anim.planeModel = planeObj.transform;
    }

    #endregion

    #region 꼬마버스 세팅

    private static BusController SetupBus(GameObject parent, Vector3 spawnPos)
    {
        // 씬에 기존 버스가 있는지 확인
        BusController existingBus = UnityEngine.Object.FindAnyObjectByType<BusController>();
        if (existingBus != null)
        {
            existingBus.transform.position = spawnPos;
            return existingBus;
        }

        GameObject busObj = null;

        // 1. 임포트된 버스 프리팹 검색
        string[] busCandidates = {
            "Assets/SimplePoly City - Low Poly Assets/Prefab/Vehicles/Vehicle with Static Wheels/Vehicle_Bus_color01.prefab",
            "Assets/SimplePoly City - Low Poly Assets/Prefab/Vehicles/Vehicle with Static Wheels/Vehicle_Bus_color02.prefab",
            "Assets/Vladislav Simakov/Street_Vehicles_Pack_Autobus_Tram/Prefabs/SmallAutobus_Mesh.prefab"
        };

        foreach (var path in busCandidates)
        {
            GameObject busPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (busPrefab != null)
            {
                busObj = (GameObject)PrefabUtility.InstantiatePrefab(busPrefab, parent.transform);
                break;
            }
        }

        if (busObj == null)
        {
            // 폴백 버스 큐브
            busObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            busObj.name = "CoCoLittleBus";
            busObj.transform.SetParent(parent.transform);
            busObj.transform.localScale = new Vector3(2.4f, 2.2f, 5.5f);
            busObj.GetComponent<MeshRenderer>().sharedMaterial = GetOrCreateMaterial("Mat_BusBody", new Color(0.2f, 0.65f, 1.0f)); // 친근한 파란색 버스
        }
        else
        {
            busObj.name = "CoCoLittleBus";
        }

        busObj.transform.position = spawnPos + Vector3.up * 1.0f;
        busObj.transform.rotation = Quaternion.identity;

        // Rigidbody 설정
        Rigidbody rb = busObj.GetComponent<Rigidbody>();
        if (rb == null) rb = busObj.AddComponent<Rigidbody>();
        rb.mass = 1200f;
        rb.linearDamping = 0.5f;
        rb.angularDamping = 1.0f;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        // BoxCollider 설정
        BoxCollider boxCol = busObj.GetComponent<BoxCollider>();
        if (boxCol == null)
        {
            boxCol = busObj.AddComponent<BoxCollider>();
            boxCol.size = new Vector3(2.4f, 2.2f, 5.5f);
            boxCol.center = new Vector3(0, 1.1f, 0);
        }

        BusController busController = busObj.GetComponent<BusController>();
        if (busController == null) busController = busObj.AddComponent<BusController>();

        // 2. 전면 2.5D 표정 스티커/스프라이트 부착
        GameObject faceObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
        faceObj.name = "BusFrontFace";
        faceObj.transform.SetParent(busObj.transform);
        faceObj.transform.localPosition = new Vector3(0f, 1.35f, 2.76f);
        faceObj.transform.localRotation = Quaternion.identity;
        faceObj.transform.localScale = new Vector3(1.8f, 1.2f, 1f);
        UnityEngine.Object.DestroyImmediate(faceObj.GetComponent<Collider>());

        Material faceMat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default"));
        Texture2D normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(Path.Combine(BusFacesDir, "BusFace_Normal.png"));
        Texture2D winkTex = AssetDatabase.LoadAssetAtPath<Texture2D>(Path.Combine(BusFacesDir, "BusFace_Wink.png"));
        Texture2D chargingTex = AssetDatabase.LoadAssetAtPath<Texture2D>(Path.Combine(BusFacesDir, "BusFace_Charging.png"));

        faceMat.mainTexture = normalTex;
        faceObj.GetComponent<MeshRenderer>().sharedMaterial = faceMat;

        busController.faceRenderer = faceObj.GetComponent<MeshRenderer>();
        busController.normalFaceTexture = normalTex;
        busController.winkFaceTexture = winkTex;
        busController.happyChargingFaceTexture = chargingTex;

        // 3. 주행 먼지 및 경적 파티클
        GameObject dustObj = new GameObject("DustParticle");
        dustObj.transform.SetParent(busObj.transform);
        dustObj.transform.localPosition = new Vector3(0, 0.2f, -2.6f);
        ParticleSystem dustPS = dustObj.AddComponent<ParticleSystem>();
        var dustMain = dustPS.main;
        dustMain.playOnAwake = false;
        dustMain.startSize = 0.6f;
        dustMain.startLifetime = 0.8f;
        dustMain.startColor = new Color(0.85f, 0.85f, 0.85f, 0.45f);
        busController.dustParticle = dustPS;

        GameObject hornObj = new GameObject("HornParticle");
        hornObj.transform.SetParent(busObj.transform);
        hornObj.transform.localPosition = new Vector3(0, 2.2f, 0);
        ParticleSystem hornPS = hornObj.AddComponent<ParticleSystem>();
        var hornMain = hornPS.main;
        hornMain.playOnAwake = false;
        hornMain.loop = false;
        hornMain.startSize = 0.8f;
        hornMain.startLifetime = 1.0f;
        hornMain.startColor = new Color(1f, 0.4f, 0.7f);
        busController.hornHeartParticle = hornPS;

        return busController;
    }

    private static void SetupCamera(Transform busTarget)
    {
        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            GameObject camObj = new GameObject("Main Camera");
            mainCam = camObj.AddComponent<Camera>();
            camObj.tag = "MainCamera";
        }

        SmoothCameraFollow follow = mainCam.GetComponent<SmoothCameraFollow>();
        if (follow == null) follow = mainCam.gameObject.AddComponent<SmoothCameraFollow>();
        follow.target = busTarget;
        follow.offset = new Vector3(0f, 9.5f, -13.5f);
    }

    #endregion

    #region 가족 승객 2.5D 빌보드 생성

    private static void SetupFamilyPassengers(GameObject parent, Dictionary<string, Landmark> landmarks, TMP_FontAsset fontAsset)
    {
        GameObject passengersRoot = new GameObject("FamilyPassengers");
        passengersRoot.transform.SetParent(parent.transform);

        // 가족 승객 데이터 매핑 (PRD 기준)
        var passengerConfigs = new[]
        {
            new { Name = "선율이", SpawnLM = "아이파크캐슬", DestLM = "나라유치원", TexFile = "선율.png" },
            new { Name = "태율이", SpawnLM = "아이파크캐슬", DestLM = "코코어린이집", TexFile = "태율.jpg" },
            new { Name = "엄마",   SpawnLM = "아이파크캐슬", DestLM = "하남 스타필드", TexFile = "엄마.png" },
            new { Name = "아빠",   SpawnLM = "아이파크캐슬", DestLM = "글빛공원", TexFile = "아빠.png" },
            new { Name = "친할머니", SpawnLM = "왕십리 삼성아파트", DestLM = "아이파크캐슬", TexFile = "친할머니.png" },
            new { Name = "친할아버지", SpawnLM = "왕십리 삼성아파트", DestLM = "에버랜드", TexFile = "친할아버지.png" },
            new { Name = "외할머니", SpawnLM = "하남 풍산 아이파크", DestLM = "롯데월드", TexFile = "외할머니.png" },
            new { Name = "외할아버지", SpawnLM = "하남 풍산 아이파크", DestLM = "인천공항", TexFile = "외할아버지.png" }
        };

        int offsetCounter = 0;
        foreach (var cfg in passengerConfigs)
        {
            if (!landmarks.ContainsKey(cfg.SpawnLM)) continue;

            Vector3 spawnPos = landmarks[cfg.SpawnLM].transform.position + new Vector3((offsetCounter % 3) * 2.5f - 2.5f, 0f, 4.5f);
            offsetCounter++;

            CreatePassengerObject(passengersRoot.transform, cfg.Name, cfg.DestLM, cfg.TexFile, spawnPos, fontAsset);
        }
    }

    private static void CreatePassengerObject(Transform parent, string name, string destination, string texFileName, Vector3 pos, TMP_FontAsset fontAsset)
    {
        GameObject pObj = new GameObject($"Passenger_{name}");
        pObj.transform.SetParent(parent);
        pObj.transform.position = pos;

        Passenger passenger = pObj.AddComponent<Passenger>();
        passenger.passengerName = name;
        passenger.destinationLandmarkName = destination;

        // 트리거 콜라이더
        SphereCollider trigger = pObj.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = 2.4f;

        // Visual Root (2.5D 빌보드 회전용)
        GameObject visualRoot = new GameObject("VisualRoot");
        visualRoot.transform.SetParent(pObj.transform);
        visualRoot.transform.localPosition = Vector3.zero;
        passenger.visualRoot = visualRoot.transform;

        // 초상화 Quad
        GameObject portraitQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        portraitQuad.name = "PortraitQuad";
        portraitQuad.transform.SetParent(visualRoot.transform);
        portraitQuad.transform.localPosition = new Vector3(0, 1.2f, 0);
        portraitQuad.transform.localScale = new Vector3(1.6f, 1.6f, 1f);
        UnityEngine.Object.DestroyImmediate(portraitQuad.GetComponent<Collider>());

        // 텍스처 연결
        string texPath = Path.Combine(FamilyTexturesDir, texFileName);
        Texture2D faceTex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        Material portraitMat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default"));
        if (faceTex != null)
        {
            portraitMat.mainTexture = faceTex;
            passenger.faceTexture = faceTex;
        }
        portraitQuad.GetComponent<MeshRenderer>().sharedMaterial = portraitMat;
        passenger.portraitRenderer = portraitQuad.GetComponent<MeshRenderer>();

        // 머리 위 이름표 TMP
        GameObject nameObj = new GameObject("NameTag");
        nameObj.transform.SetParent(visualRoot.transform);
        nameObj.transform.localPosition = new Vector3(0, 2.3f, 0);
        TextMeshPro nameTMP = nameObj.AddComponent<TextMeshPro>();
        nameTMP.text = $"{name}\n<size=70%><color=#FFE680>▶ {destination}</color></size>";
        nameTMP.fontSize = 4.2f;
        nameTMP.alignment = TextAlignmentOptions.Center;
        nameTMP.color = Color.white;
        nameTMP.fontStyle = FontStyles.Bold;
        if (fontAsset != null) nameTMP.font = fontAsset;
        passenger.nameTagText = nameTMP;

        // 승객 발밑 반짝이 파티클
        GameObject sparkleObj = new GameObject("SparkleParticle");
        sparkleObj.transform.SetParent(visualRoot.transform);
        sparkleObj.transform.localPosition = new Vector3(0, 0.2f, 0);
        ParticleSystem ps = sparkleObj.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.playOnAwake = true;
        main.startSize = 0.35f;
        main.startLifetime = 1.2f;
        main.startColor = new Color(1f, 0.9f, 0.4f, 0.8f);
        var emission = ps.emission;
        emission.rateOverTime = 8f;
        passenger.sparkleParticle = ps;
    }

    #endregion

    #region 모바일 UI & 미니게임 팝업

    private static void SetupUI(BusController bus, TMP_FontAsset fontAsset)
    {
        // 캔버스 생성
        GameObject canvasObj = GameObject.Find("GameCanvas");
        if (canvasObj == null)
        {
            canvasObj = new GameObject("GameCanvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(2560, 1600); // 갤럭시 탭 S9 울트라 (16:10 해상도 최적화)
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();
        }

        // EventSystem 확인 및 New Input System 모듈 연결
        EventSystem es = UnityEngine.Object.FindAnyObjectByType<EventSystem>();
        if (es == null)
        {
            GameObject esObj = new GameObject("EventSystem");
            es = esObj.AddComponent<EventSystem>();
        }

        StandaloneInputModule oldMod = es.GetComponent<StandaloneInputModule>();
        if (oldMod != null)
        {
            UnityEngine.Object.DestroyImmediate(oldMod);
        }

        Type newModType = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (newModType != null && es.GetComponent(newModType) == null)
        {
            es.gameObject.AddComponent(newModType);
        }

        GameHUD hud = canvasObj.GetComponent<GameHUD>() ?? canvasObj.AddComponent<GameHUD>();
        MobileTouchControls controls = canvasObj.GetComponent<MobileTouchControls>() ?? canvasObj.AddComponent<MobileTouchControls>();

        // 1. 상단 미션 안내 바
        SetupTopMissionHUD(canvasObj.transform, hud, fontAsset);

        // 2. 모바일 터치 페달 및 조향 버튼 (가속, 브레이크, 좌/우, 빵빵)
        SetupMobileTouchHUD(canvasObj.transform, hud, controls, fontAsset);

        // 3. 미니게임 퀴즈 모달 팝업
        SetupMiniGameModal(canvasObj.transform, fontAsset);
    }

    private static void SetupTopMissionHUD(Transform canvasTransform, GameHUD hud, TMP_FontAsset fontAsset)
    {
        // 상단 컨테이너
        GameObject topBar = new GameObject("TopMissionBar");
        topBar.transform.SetParent(canvasTransform, false);
        RectTransform rt = topBar.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.1f, 0.88f);
        rt.anchorMax = new Vector2(0.9f, 0.98f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        Image bg = topBar.AddComponent<Image>();
        bg.color = new Color(0.1f, 0.15f, 0.25f, 0.85f);

        // 미션 텍스트
        GameObject missionTextObj = new GameObject("MissionText");
        missionTextObj.transform.SetParent(topBar.transform, false);
        RectTransform textRT = missionTextObj.AddComponent<RectTransform>();
        textRT.anchorMin = new Vector2(0.02f, 0.1f);
        textRT.anchorMax = new Vector2(0.65f, 0.9f);
        textRT.offsetMin = textRT.offsetMax = Vector2.zero;

        TextMeshProUGUI missionTMP = missionTextObj.AddComponent<TextMeshProUGUI>();
        missionTMP.text = "아이파크캐슬에서 선율이와 태율이를 태워주세요! 🚌";
        missionTMP.fontSize = 42;
        missionTMP.color = Color.white;
        missionTMP.alignment = TextAlignmentOptions.MidlineLeft;
        if (fontAsset != null) missionTMP.font = fontAsset;
        hud.missionTextTMP = missionTMP;

        // 배터리 잔량 & 속도 표시
        GameObject statusObj = new GameObject("StatusGroup");
        statusObj.transform.SetParent(topBar.transform, false);
        RectTransform statusRT = statusObj.AddComponent<RectTransform>();
        statusRT.anchorMin = new Vector2(0.68f, 0.1f);
        statusRT.anchorMax = new Vector2(0.98f, 0.9f);
        statusRT.offsetMin = statusRT.offsetMax = Vector2.zero;

        TextMeshProUGUI statusTMP = statusObj.AddComponent<TextMeshProUGUI>();
        statusTMP.text = "100% ⚡  |  0 km/h";
        statusTMP.fontSize = 38;
        statusTMP.color = new Color(0.4f, 1f, 0.5f);
        statusTMP.alignment = TextAlignmentOptions.MidlineRight;
        if (fontAsset != null) statusTMP.font = fontAsset;
        hud.batteryTextTMP = statusTMP;

        // 토스트 알림 패널
        GameObject toast = new GameObject("ToastPanel");
        toast.transform.SetParent(canvasTransform, false);
        RectTransform toastRT = toast.AddComponent<RectTransform>();
        toastRT.anchorMin = new Vector2(0.25f, 0.75f);
        toastRT.anchorMax = new Vector2(0.75f, 0.84f);
        toastRT.offsetMin = toastRT.offsetMax = Vector2.zero;

        Image toastBG = toast.AddComponent<Image>();
        toastBG.color = new Color(1f, 0.9f, 0.2f, 0.95f);

        GameObject toastTextObj = new GameObject("ToastText");
        toastTextObj.transform.SetParent(toast.transform, false);
        RectTransform ttRT = toastTextObj.AddComponent<RectTransform>();
        ttRT.anchorMin = Vector2.zero;
        ttRT.anchorMax = Vector2.one;
        ttRT.offsetMin = ttRT.offsetMax = Vector2.zero;

        TextMeshProUGUI toastTMP = toastTextObj.AddComponent<TextMeshProUGUI>();
        toastTMP.text = "선율이 탑승 완료!";
        toastTMP.fontSize = 44;
        toastTMP.color = new Color(0.15f, 0.15f, 0.15f);
        toastTMP.alignment = TextAlignmentOptions.Center;
        if (fontAsset != null) toastTMP.font = fontAsset;

        hud.toastPanel = toast;
        hud.toastTextTMP = toastTMP;
        toast.SetActive(false);
    }

    private static void SetupMobileTouchHUD(Transform canvasTransform, GameHUD hud, MobileTouchControls controls, TMP_FontAsset fontAsset)
    {
        // 1. 좌측 조향 버튼 (좌/우)
        CreateTouchButton(canvasTransform, "Btn_Left", "◀", new Vector2(0.04f, 0.05f), new Vector2(0.14f, 0.21f), new Color(0.3f, 0.65f, 1f, 0.8f), fontAsset,
            controls.OnLeftDown, controls.OnLeftUp);

        CreateTouchButton(canvasTransform, "Btn_Right", "▶", new Vector2(0.16f, 0.05f), new Vector2(0.26f, 0.21f), new Color(0.3f, 0.65f, 1f, 0.8f), fontAsset,
            controls.OnRightDown, controls.OnRightUp);

        // 2. 우측 페달 버튼 (가속 / 브레이크)
        CreateTouchButton(canvasTransform, "Btn_Gas", "가속\n🚀", new Vector2(0.85f, 0.05f), new Vector2(0.96f, 0.24f), new Color(0.2f, 0.85f, 0.35f, 0.85f), fontAsset,
            controls.OnGasDown, controls.OnGasUp);

        CreateTouchButton(canvasTransform, "Btn_Brake", "정지\n🛑", new Vector2(0.72f, 0.05f), new Vector2(0.83f, 0.21f), new Color(1f, 0.35f, 0.35f, 0.85f), fontAsset,
            controls.OnBrakeDown, controls.OnBrakeUp);

        // 3. 중앙 하단 경적 버튼 ("빵빵! 📢")
        GameObject hornObj = CreateTouchButton(canvasTransform, "Btn_Horn", "빵빵! 📢", new Vector2(0.44f, 0.04f), new Vector2(0.56f, 0.18f), new Color(1f, 0.75f, 0.15f, 0.9f), fontAsset,
            controls.OnHornClicked, null);

        hud.hornButton = hornObj.GetComponent<Button>();
    }

    private static GameObject CreateTouchButton(Transform parent, string name, string label, Vector2 anchorMin, Vector2 anchorMax, Color color, TMP_FontAsset fontAsset, Action onDown, Action onUp)
    {
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent, false);
        RectTransform rt = btnObj.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        Image img = btnObj.AddComponent<Image>();
        img.color = color;

        Button btn = btnObj.AddComponent<Button>();

        // EventTrigger를 통한 누르고 있기 감지
        EventTrigger trigger = btnObj.AddComponent<EventTrigger>();
        if (onDown != null)
        {
            EventTrigger.Entry entryDown = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            entryDown.callback.AddListener((data) => onDown.Invoke());
            trigger.triggers.Add(entryDown);
        }
        if (onUp != null)
        {
            EventTrigger.Entry entryUp = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
            entryUp.callback.AddListener((data) => onUp.Invoke());
            trigger.triggers.Add(entryUp);
        }

        // 라벨 텍스트
        GameObject txtObj = new GameObject("Label");
        txtObj.transform.SetParent(btnObj.transform, false);
        RectTransform txtRT = txtObj.AddComponent<RectTransform>();
        txtRT.anchorMin = Vector2.zero;
        txtRT.anchorMax = Vector2.one;
        txtRT.offsetMin = txtRT.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = txtObj.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 40;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        if (fontAsset != null) tmp.font = fontAsset;

        return btnObj;
    }

    private static void SetupMiniGameModal(Transform canvasTransform, TMP_FontAsset fontAsset)
    {
        GameObject modalObj = new GameObject("MiniGameModal");
        modalObj.transform.SetParent(canvasTransform, false);
        RectTransform rt = modalObj.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        Image dimmer = modalObj.AddComponent<Image>();
        dimmer.color = new Color(0f, 0f, 0f, 0.72f);

        MiniGameUI miniGame = modalObj.AddComponent<MiniGameUI>();
        miniGame.modalRoot = modalObj;

        // 팝업 카드 컨테이너
        GameObject cardBox = new GameObject("CardBox");
        cardBox.transform.SetParent(modalObj.transform, false);
        RectTransform boxRT = cardBox.AddComponent<RectTransform>();
        boxRT.anchorMin = new Vector2(0.2f, 0.2f);
        boxRT.anchorMax = new Vector2(0.8f, 0.8f);
        boxRT.offsetMin = boxRT.offsetMax = Vector2.zero;

        Image boxBG = cardBox.AddComponent<Image>();
        boxBG.color = new Color(0.98f, 0.98f, 0.95f);

        // 타이틀
        GameObject titleObj = new GameObject("ModalTitle");
        titleObj.transform.SetParent(cardBox.transform, false);
        RectTransform tRT = titleObj.AddComponent<RectTransform>();
        tRT.anchorMin = new Vector2(0.05f, 0.82f);
        tRT.anchorMax = new Vector2(0.95f, 0.96f);
        tRT.offsetMin = tRT.offsetMax = Vector2.zero;

        TextMeshProUGUI titleTMP = titleObj.AddComponent<TextMeshProUGUI>();
        titleTMP.text = "⭐ 신나는 도착 퀴즈! ⭐";
        titleTMP.fontSize = 54;
        titleTMP.color = new Color(0.2f, 0.2f, 0.35f);
        titleTMP.alignment = TextAlignmentOptions.Center;
        if (fontAsset != null) titleTMP.font = fontAsset;
        miniGame.titleTMP = titleTMP;

        // 질문 텍스트
        GameObject qObj = new GameObject("ModalQuestion");
        qObj.transform.SetParent(cardBox.transform, false);
        RectTransform qRT = qObj.AddComponent<RectTransform>();
        qRT.anchorMin = new Vector2(0.05f, 0.65f);
        qRT.anchorMax = new Vector2(0.95f, 0.80f);
        qRT.offsetMin = qRT.offsetMax = Vector2.zero;

        TextMeshProUGUI qTMP = qObj.AddComponent<TextMeshProUGUI>();
        qTMP.text = "어디에 도착했을까요? 알맞은 카드를 터치해 보세요!";
        qTMP.fontSize = 42;
        qTMP.color = new Color(0.3f, 0.35f, 0.45f);
        qTMP.alignment = TextAlignmentOptions.Center;
        if (fontAsset != null) qTMP.font = fontAsset;
        miniGame.questionTMP = qTMP;

        // 3개 선택 카드 버튼
        Color[] cardColors = {
            new Color(0.35f, 0.75f, 1f),
            new Color(1f, 0.65f, 0.25f),
            new Color(0.4f, 0.85f, 0.45f)
        };

        for (int i = 0; i < 3; i++)
        {
            float minX = 0.08f + i * 0.30f;
            float maxX = minX + 0.24f;

            GameObject card = new GameObject($"Card_{i}");
            card.transform.SetParent(cardBox.transform, false);
            RectTransform cRT = card.AddComponent<RectTransform>();
            cRT.anchorMin = new Vector2(minX, 0.22f);
            cRT.anchorMax = new Vector2(maxX, 0.58f);
            cRT.offsetMin = cRT.offsetMax = Vector2.zero;

            Image cImg = card.AddComponent<Image>();
            cImg.color = cardColors[i];

            Button cBtn = card.AddComponent<Button>();
            miniGame.cardButtons.Add(cBtn);

            GameObject cTxtObj = new GameObject("Text");
            cTxtObj.transform.SetParent(card.transform, false);
            RectTransform ctRT = cTxtObj.AddComponent<RectTransform>();
            ctRT.anchorMin = Vector2.zero;
            ctRT.anchorMax = Vector2.one;
            ctRT.offsetMin = ctRT.offsetMax = Vector2.zero;

            TextMeshProUGUI cTMP = cTxtObj.AddComponent<TextMeshProUGUI>();
            cTMP.text = $"보기 {i + 1}";
            cTMP.fontSize = 44;
            cTMP.fontStyle = FontStyles.Bold;
            cTMP.alignment = TextAlignmentOptions.Center;
            cTMP.color = Color.white;
            if (fontAsset != null) cTMP.font = fontAsset;
            miniGame.cardTexts.Add(cTMP);
        }

        // 축하 텍스트
        GameObject celObj = new GameObject("CelebrationText");
        celObj.transform.SetParent(cardBox.transform, false);
        RectTransform celRT = celObj.AddComponent<RectTransform>();
        celRT.anchorMin = new Vector2(0.05f, 0.05f);
        celRT.anchorMax = new Vector2(0.95f, 0.18f);
        celRT.offsetMin = celRT.offsetMax = Vector2.zero;

        TextMeshProUGUI celTMP = celObj.AddComponent<TextMeshProUGUI>();
        celTMP.text = "🎉 정답이에요! 참 잘했어요! ⭐⭐⭐";
        celTMP.fontSize = 46;
        celTMP.color = new Color(0.95f, 0.25f, 0.4f);
        celTMP.alignment = TextAlignmentOptions.Center;
        if (fontAsset != null) celTMP.font = fontAsset;
        miniGame.celebrationTMP = celTMP;
        celObj.SetActive(false);

        modalObj.SetActive(false);
    }

    #endregion

    #region 매니저 설정

    private static void SetupManagers(GameObject parent)
    {
        GameObject mgrObj = GameObject.Find("PassengerManager");
        if (mgrObj == null)
        {
            mgrObj = new GameObject("PassengerManager");
            mgrObj.transform.SetParent(parent.transform);
        }

        if (mgrObj.GetComponent<PassengerManager>() == null)
        {
            mgrObj.AddComponent<PassengerManager>();
        }
    }

    #endregion
}

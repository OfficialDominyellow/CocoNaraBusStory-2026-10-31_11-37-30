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
    private const string UITexturesDir = "Assets/Textures/UI";
    private const string DemoScenePath = "Assets/SimplePoly City - Low Poly Assets/Demo/SimplePoly City - Low Poly Assets_Demo Scene.unity";
    private const string TargetScenePath = "Assets/Scenes/SampleScene.unity";

    [MenuItem("CocoNara/1. ★ [최고 퀄리티] 실제 3D 도시 오픈월드 씬 완전 구축 (Build High-End City)", false, 1)]
    public static void BuildCompleteGameScene()
    {
        Debug.Log("<color=#00FF88>[CocoNara] 최고 퀄리티 3D 오픈월드 도시 씬 구축을 시작합니다...</color>");

        // 1. 필요한 텍스처(표정, UI, 가족 뱃지) 및 TMP 폰트 준비
        EnsureDirectories();
        GenerateBusFaceTextures();
        EnsureFamilyMemberTextures();
        TMP_FontAsset fontAsset = GetOrCreateTMPFontAsset();

        // 2. 핑크색 머티리얼 URP Lit으로 일괄 변환 (모든 애셋 정상 렌더링)
        ConvertAllLegacyMaterialsToURP();

        // 3. 실제 정밀 모델링된 3D 도시 맵(SimplePoly City Demo)을 SampleScene으로 로드/동기화
        EnsureCityBaseScene();

        // 4. 기존 임시 오브젝트 정리
        CleanExistingGameplayObjects();

        // 5. 게임 시스템 루트 생성
        GameObject gameRoot = GetOrCreateRoot("CocoNara_GameSystems");

        // 6. 12개 실제 도시 랜드마크 & 3D 테마 간판 배치
        Dictionary<string, Landmark> landmarks = SetupHighQualityLandmarks(gameRoot.transform, fontAsset);

        // 7. 꼬마버스 스폰 (도로 한가운데 탁 트인 직선 도로 위 완벽 배치)
        Vector3 busSpawnRoadPos = new Vector3(60f, 0.45f, 40f); // 2차선 아스팔트 대로 위
        Quaternion busSpawnRot = Quaternion.Euler(0f, 90f, 0f); // 앞쪽 시원하게 뚫린 도로 방향
        BusController bus = SetupHighQualityBus(gameRoot.transform, busSpawnRoadPos, busSpawnRot);

        // 8. 시야 방해 없는 스마트 쿼터뷰 추적 카메라 세팅
        SetupSmartCamera(bus.transform);

        // 9. 2.5D 가족 승객 정류장 인도 위 배치
        SetupFamilyPassengers(gameRoot.transform, landmarks, fontAsset);

        // 10. 상용 게임급 갤럭시 탭 S9 울트라 모바일 UI 구축
        SetupCommercialGradeUI(bus, fontAsset);

        // 11. 승객 & 미션 매니저 세팅
        SetupManagers(gameRoot.transform);

        // 12. New Input System 모듈 완벽 호환 보장
        FixEventSystemInputModule();

        // 씬 저장
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log("<color=#00FF88>[CocoNara] 🎉 3D 실제 도시 기반 최고 퀄리티 게임 씬이 성공적으로 구축되었습니다!</color>");
        if (!Application.isBatchMode)
        {
            EditorUtility.DisplayDialog("CocoNaraBusStory", "🎉 코코나라 꼬마버스 고퀄리티 오픈월드 씬이 구축되었습니다!\n\n1. 실제 3D 저폴리 도시 도로/건물/가로등/나무가 적용되었습니다.\n2. 버스가 건물 뒤가 아닌 탁 트인 대로 한가운데 도로 위에서 출발합니다.\n3. 머리 위 3D 나침반 화살표가 목적지를 안내합니다.\n4. 상용 키즈 게임급 둥근 UI와 빵빵 경적이 적용되었습니다.\n\n▶ Play 버튼을 눌러 테스트해보세요!", "확인");
        }
    }

    private static void EnsureDirectories()
    {
        if (!Directory.Exists(BusFacesDir)) Directory.CreateDirectory(BusFacesDir);
        if (!Directory.Exists(FamilyTexturesDir)) Directory.CreateDirectory(FamilyTexturesDir);
        if (!Directory.Exists(UITexturesDir)) Directory.CreateDirectory(UITexturesDir);
        AssetDatabase.Refresh();
    }

    private static void EnsureCityBaseScene()
    {
        if (File.Exists(DemoScenePath))
        {
            // DemoScene을 SampleScene으로 복사하여 완벽한 도시 환경 확보
            string currentScenePath = EditorSceneManager.GetActiveScene().path;
            if (currentScenePath != DemoScenePath && currentScenePath != TargetScenePath)
            {
                File.Copy(DemoScenePath, TargetScenePath, true);
                AssetDatabase.ImportAsset(TargetScenePath);
                EditorSceneManager.OpenScene(TargetScenePath);
            }
        }
    }

    private static void CleanExistingGameplayObjects()
    {
        // 이전의 어설픈 큐브 기반 오브젝트들 제거
        string[] oldNames = { "WorldRoot", "CocoNara_GameSystems", "GameCanvas", "Main Camera (Old)" };
        foreach (var name in oldNames)
        {
            GameObject obj = GameObject.Find(name);
            if (obj != null)
            {
                UnityEngine.Object.DestroyImmediate(obj);
            }
        }

        // 데모 씬의 기본 고정 카메라는 우리 스마트 카메라로 대체
        Camera[] cams = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
        foreach (var c in cams)
        {
            if (c.gameObject.name == "Main Camera" && c.GetComponent<SmoothCameraFollow>() == null)
            {
                UnityEngine.Object.DestroyImmediate(c.gameObject);
            }
        }
    }

    private static GameObject GetOrCreateRoot(string name)
    {
        GameObject obj = GameObject.Find(name);
        if (obj == null)
        {
            obj = new GameObject(name);
        }
        return obj;
    }

    #region 꼬마버스 세팅 (실제 분리 휠 3D 모델 & 2.5D 표정 & 라이트)

    private static BusController SetupHighQualityBus(Transform parent, Vector3 spawnPos, Quaternion spawnRot)
    {
        // 분리형 바퀴 버스 프리팹 사용
        string busPrefabPath = "Assets/SimplePoly City - Low Poly Assets/Prefab/Vehicles/Vehicle with Separated Wheels/Vehicle_Bus_color01_separate.prefab";
        GameObject busPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(busPrefabPath);

        GameObject busObj = null;
        if (busPrefab != null)
        {
            busObj = (GameObject)PrefabUtility.InstantiatePrefab(busPrefab, parent);
        }
        else
        {
            busObj = new GameObject("CoCoLittleBus");
            busObj.transform.SetParent(parent);
        }

        busObj.name = "CoCoLittleBus";
        busObj.transform.position = spawnPos;
        busObj.transform.rotation = spawnRot;

        // 1. Rigidbody 세팅 (낮은 무게중심, 전복 방지, 스무스 아케이드)
        Rigidbody rb = busObj.GetComponent<Rigidbody>() ?? busObj.AddComponent<Rigidbody>();
        rb.mass = 1200f;
        rb.linearDamping = 0.4f;
        rb.angularDamping = 1.0f;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        rb.centerOfMass = new Vector3(0f, -0.7f, 0f);

        // 2. BoxCollider 세팅 (턱 걸림 방지 부드러운 하단)
        BoxCollider boxCol = busObj.GetComponent<BoxCollider>() ?? busObj.AddComponent<BoxCollider>();
        boxCol.size = new Vector3(2.6f, 2.8f, 8.2f);
        boxCol.center = new Vector3(0f, 1.55f, 0f);

        // 턱에 걸리지 않는 무마찰 물리 재질
        PhysicMaterial smoothMat = new PhysicMaterial("BusFrictionless")
        {
            dynamicFriction = 0.05f,
            staticFriction = 0.05f,
            frictionCombine = PhysicMaterialCombine.Minimum,
            bounciness = 0f
        };
        boxCol.material = smoothMat;

        // 3. BusController 컴포넌트 세팅
        BusController busController = busObj.GetComponent<BusController>() ?? busObj.AddComponent<BusController>();
        busController.maxForwardSpeed = 22f;
        busController.maxReverseSpeed = 10f;
        busController.acceleration = 22f;
        busController.deceleration = 20f;
        busController.steerSpeed = 115f;

        // 바퀴 연결
        busController.rotatingWheels.Clear();
        busController.steeringFrontWheels.Clear();

        Transform wheelRR = busObj.transform.Find("Wheel_RR");
        Transform wheelRL = busObj.transform.Find("Wheel_RL");
        Transform wheelFR = busObj.transform.Find("Wheel_FR");
        Transform wheelFL = busObj.transform.Find("Wheel_FL");

        if (wheelRR != null) busController.rotatingWheels.Add(wheelRR);
        if (wheelRL != null) busController.rotatingWheels.Add(wheelRL);
        if (wheelFR != null) { busController.rotatingWheels.Add(wheelFR); busController.steeringFrontWheels.Add(wheelFR); }
        if (wheelFL != null) { busController.rotatingWheels.Add(wheelFL); busController.steeringFrontWheels.Add(wheelFL); }

        // 4. 전면 2.5D 표정 스티커 부착 (앞유리 위)
        Transform existingFace = busObj.transform.Find("BusFrontFace");
        if (existingFace != null) UnityEngine.Object.DestroyImmediate(existingFace.gameObject);

        GameObject faceObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
        faceObj.name = "BusFrontFace";
        faceObj.transform.SetParent(busObj.transform);
        faceObj.transform.localPosition = new Vector3(0f, 2.05f, 4.35f);
        faceObj.transform.localRotation = Quaternion.identity;
        faceObj.transform.localScale = new Vector3(2.1f, 1.35f, 1f);
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

        // 5. 전조등 (밤길이나 터널에서도 밝게 비추는 따뜻한 헤드라이트 2구)
        CreateHeadlight(busObj.transform, new Vector3(-0.95f, 0.95f, 4.25f));
        CreateHeadlight(busObj.transform, new Vector3(0.95f, 0.95f, 4.25f));

        // 6. 3D 내비게이션 화살표 부착 (아이들이 길을 잃지 않도록 머리 위에서 목적지를 가리킴)
        Create3DNavigationPointer(busObj.transform);

        return busController;
    }

    private static void CreateHeadlight(Transform parent, Vector3 localPos)
    {
        GameObject lightObj = new GameObject("Headlight");
        lightObj.transform.SetParent(parent);
        lightObj.transform.localPosition = localPos;
        lightObj.transform.localRotation = Quaternion.Euler(10f, 0f, 0f);

        Light light = lightObj.AddComponent<Light>();
        light.type = LightType.Spot;
        light.range = 28f;
        light.spotAngle = 55f;
        light.intensity = 2.5f;
        light.color = new Color(1f, 0.95f, 0.75f);
    }

    private static void Create3DNavigationPointer(Transform busTransform)
    {
        GameObject pointerRoot = new GameObject("NavigationPointer");
        pointerRoot.transform.SetParent(busTransform);
        pointerRoot.transform.localPosition = new Vector3(0f, 4.3f, 0f);

        DestinationPointer dp = pointerRoot.AddComponent<DestinationPointer>();

        // 황금빛 3D 화살표 모델
        GameObject arrowObj = new GameObject("ArrowVisual");
        arrowObj.transform.SetParent(pointerRoot.transform);
        arrowObj.transform.localPosition = Vector3.zero;

        // 화살촉 (콘/피라미드)
        GameObject cone = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cone.name = "ArrowShaft";
        cone.transform.SetParent(arrowObj.transform);
        cone.transform.localScale = new Vector3(0.5f, 1.2f, 0.5f);
        cone.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        cone.transform.localPosition = new Vector3(0f, 0f, -0.4f);

        Material arrowMat = GetOrCreateMaterial("Mat_NavArrow_Gold", new Color(1f, 0.85f, 0.15f));
        cone.GetComponent<MeshRenderer>().sharedMaterial = arrowMat;
        UnityEngine.Object.DestroyImmediate(cone.GetComponent<Collider>());

        GameObject head = GameObject.CreatePrimitive(PrimitiveType.Cube);
        head.name = "ArrowHead";
        head.transform.SetParent(arrowObj.transform);
        head.transform.localScale = new Vector3(1.1f, 0.35f, 1.1f);
        head.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
        head.transform.localPosition = new Vector3(0f, 0f, 0.8f);
        head.GetComponent<MeshRenderer>().sharedMaterial = arrowMat;
        UnityEngine.Object.DestroyImmediate(head.GetComponent<Collider>());

        dp.arrowVisual = arrowObj.transform;
    }

    #endregion

    #region 스마트 쿼터뷰 카메라

    private static void SetupSmartCamera(Transform busTarget)
    {
        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            GameObject camObj = new GameObject("Main Camera");
            mainCam = camObj.AddComponent<Camera>();
            camObj.tag = "MainCamera";
        }

        SmoothCameraFollow follow = mainCam.GetComponent<SmoothCameraFollow>() ?? mainCam.gameObject.AddComponent<SmoothCameraFollow>();
        follow.target = busTarget;
        follow.offset = new Vector3(0f, 11f, -15.5f);
        follow.lookAheadDistance = 4.5f;
        follow.SnapToTarget();
    }

    #endregion

    #region 12개 실제 도시 랜드마크 구축

    private static Dictionary<string, Landmark> SetupHighQualityLandmarks(Transform parent, TMP_FontAsset fontAsset)
    {
        GameObject lmRoot = new GameObject("Landmarks");
        lmRoot.transform.SetParent(parent);

        Dictionary<string, Landmark> dict = new Dictionary<string, Landmark>();

        // 실제 3D 도시의 주요 빌딩과 명소 좌표 매핑 (도로와 인도가 완벽히 이어지는 위치)
        dict["아이파크캐슬"] = CreateThemeLandmark(lmRoot.transform, "아이파크캐슬", "우리집 차고지 🏠", new Vector3(60f, 0f, 22f), new Color(0.95f, 0.45f, 0.2f), fontAsset, false);
        dict["코코어린이집"] = CreateThemeLandmark(lmRoot.transform, "코코어린이집", "태율이 어린이집 🍼", new Vector3(100f, 0f, 60f), new Color(1f, 0.72f, 0.15f), fontAsset, false);
        dict["나라유치원"]   = CreateThemeLandmark(lmRoot.transform, "나라유치원", "선율이 유치원 🎒", new Vector3(20f, 0f, 60f), new Color(0.35f, 0.75f, 1f), fontAsset, false);
        dict["글빛공원"]     = CreateThemeLandmark(lmRoot.transform, "글빛공원", "풍차와 벤치 공원 🌳", new Vector3(60f, 0f, 100f), new Color(0.3f, 0.85f, 0.4f), fontAsset, false);
        dict["하남 스타필드"] = CreateThemeLandmark(lmRoot.transform, "하남 스타필드", "대형 쇼핑몰 🛍️", new Vector3(120f, 0f, 120f), new Color(0.88f, 0.28f, 0.55f), fontAsset, false);
        dict["전기차 충전소"] = CreateThemeLandmark(lmRoot.transform, "전기차 충전소", "친환경 급속 충전 ⚡", new Vector3(140f, 0f, 40f), new Color(0.15f, 0.85f, 0.85f), fontAsset, true);
        dict["왕십리 삼성아파트"] = CreateThemeLandmark(lmRoot.transform, "왕십리 삼성아파트", "친할머니댁 👵👴", new Vector3(20f, 0f, 120f), new Color(0.55f, 0.42f, 0.88f), fontAsset, false);
        dict["하남 풍산 아이파크"] = CreateThemeLandmark(lmRoot.transform, "하남 풍산 아이파크", "외할머니댁 👵👴", new Vector3(160f, 0f, 80f), new Color(0.95f, 0.55f, 0.65f), fontAsset, false);
        dict["에버랜드"]     = CreateThemeLandmark(lmRoot.transform, "에버랜드", "환상의 놀이공원 🎡", new Vector3(180f, 0f, 160f), new Color(0.98f, 0.35f, 0.22f), fontAsset, false);
        dict["롯데월드"]     = CreateThemeLandmark(lmRoot.transform, "롯데월드", "모험과 신비의 나라 🎠", new Vector3(0f, 0f, 160f), new Color(0.35f, 0.55f, 0.95f), fontAsset, false);
        dict["인천공항"]     = CreateThemeLandmark(lmRoot.transform, "인천공항", "국제선 공항 🛫", new Vector3(180f, 0f, 20f), new Color(0.25f, 0.65f, 0.95f), fontAsset, false);
        dict["김포공항"]     = CreateThemeLandmark(lmRoot.transform, "김포공항", "국내선 공항 ✈️", new Vector3(0f, 0f, 20f), new Color(0.4f, 0.7f, 0.9f), fontAsset, false);

        // 인천공항 상공 선회 비행기 연출
        SetupAirportAirplane(dict["인천공항"].transform);

        return dict;
    }

    private static Landmark CreateThemeLandmark(Transform parent, string title, string subtitle, Vector3 pos, Color themeColor, TMP_FontAsset fontAsset, bool isCharging)
    {
        GameObject lmObj = new GameObject($"Landmark_{title}");
        lmObj.transform.SetParent(parent);
        lmObj.transform.position = pos;

        Landmark landmark = lmObj.AddComponent<Landmark>();
        landmark.landmarkName = title;
        landmark.subtitle = subtitle;
        landmark.isEVChargingStation = isCharging;

        // 도착 트리거
        SphereCollider trigger = lmObj.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = 9.0f;

        // 3D 테크니컬 테마 간판 루트 (지상 7m 상공에 플로팅)
        GameObject signRoot = new GameObject("SignRoot");
        signRoot.transform.SetParent(lmObj.transform);
        signRoot.transform.localPosition = new Vector3(0f, 7.2f, 0f);
        landmark.signRoot = signRoot.transform;

        // 간판 프레임 (골드 테두리 + 테마 컬러 보드)
        GameObject boardObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        boardObj.name = "ThemeSignBoard";
        boardObj.transform.SetParent(signRoot.transform);
        boardObj.transform.localPosition = Vector3.zero;
        boardObj.transform.localScale = new Vector3(8.5f, 2.5f, 0.4f);
        boardObj.GetComponent<MeshRenderer>().sharedMaterial = GetOrCreateMaterial($"Mat_Theme_{title}", themeColor);
        UnityEngine.Object.DestroyImmediate(boardObj.GetComponent<Collider>());

        // 기둥 2개
        CreateSignPillar(signRoot.transform, new Vector3(-3.8f, -3.5f, 0f));
        CreateSignPillar(signRoot.transform, new Vector3(3.8f, -3.5f, 0f));

        // 메인 타이틀 (MapleStory Bold, 선명한 흰색 볼드 텍스트)
        GameObject titleObj = new GameObject("TitleTMP");
        titleObj.transform.SetParent(signRoot.transform);
        titleObj.transform.localPosition = new Vector3(0f, 0.38f, -0.25f);
        TextMeshPro titleTMP = titleObj.AddComponent<TextMeshPro>();
        titleTMP.text = title;
        titleTMP.fontSize = 7.5f;
        titleTMP.color = Color.white;
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.fontStyle = FontStyles.Bold;
        if (fontAsset != null) titleTMP.font = fontAsset;
        landmark.signTitleTMP = titleTMP;

        // 서브타이틀
        GameObject subObj = new GameObject("SubtitleTMP");
        subObj.transform.SetParent(signRoot.transform);
        subObj.transform.localPosition = new Vector3(0f, -0.48f, -0.25f);
        TextMeshPro subTMP = subObj.AddComponent<TextMeshPro>();
        subTMP.text = subtitle;
        subTMP.fontSize = 4.8f;
        subTMP.color = new Color(1f, 0.95f, 0.55f);
        subTMP.alignment = TextAlignmentOptions.Center;
        if (fontAsset != null) subTMP.font = fontAsset;
        landmark.signSubtitleTMP = subTMP;

        // 도착 축하 파티클
        GameObject particleObj = new GameObject("ArrivalParticle");
        particleObj.transform.SetParent(lmObj.transform);
        particleObj.transform.localPosition = new Vector3(0f, 1.2f, 0f);
        ParticleSystem ps = particleObj.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.startSize = 0.55f;
        main.startLifetime = 1.6f;
        main.startColor = isCharging ? new Color(0.15f, 0.95f, 1f) : new Color(1f, 0.85f, 0.2f);
        var em = ps.emission;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, 40) });
        landmark.arrivalParticle = ps;

        return landmark;
    }

    private static void CreateSignPillar(Transform parent, Vector3 localPos)
    {
        GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pillar.name = "Pillar";
        pillar.transform.SetParent(parent);
        pillar.transform.localPosition = localPos;
        pillar.transform.localScale = new Vector3(0.3f, 3.5f, 0.3f);
        pillar.GetComponent<MeshRenderer>().sharedMaterial = GetOrCreateMaterial("Mat_Pillar_Wood", new Color(0.45f, 0.3f, 0.18f));
        UnityEngine.Object.DestroyImmediate(pillar.GetComponent<Collider>());
    }

    private static void SetupAirportAirplane(Transform airportTransform)
    {
        GameObject flightRoot = new GameObject("AirportAirplaneFlight");
        flightRoot.transform.SetParent(airportTransform);
        flightRoot.transform.localPosition = Vector3.zero;

        AirportPlaneAnimation anim = flightRoot.AddComponent<AirportPlaneAnimation>();

        GameObject planeObj = new GameObject("AirplaneModel");
        planeObj.transform.SetParent(flightRoot.transform);
        planeObj.transform.localPosition = new Vector3(0f, 16f, 0f);

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.transform.SetParent(planeObj.transform);
        body.transform.localScale = new Vector3(1.6f, 5.0f, 1.6f);
        body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        body.GetComponent<MeshRenderer>().sharedMaterial = GetOrCreateMaterial("Mat_AirplaneBody", Color.white);
        UnityEngine.Object.DestroyImmediate(body.GetComponent<Collider>());

        GameObject wing = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wing.transform.SetParent(planeObj.transform);
        wing.transform.localScale = new Vector3(10f, 0.25f, 2.2f);
        wing.transform.localPosition = new Vector3(0f, 0.2f, 0.5f);
        wing.GetComponent<MeshRenderer>().sharedMaterial = GetOrCreateMaterial("Mat_AirplaneWing", new Color(0.2f, 0.55f, 0.98f));
        UnityEngine.Object.DestroyImmediate(wing.GetComponent<Collider>());

        anim.planeModel = planeObj.transform;
    }

    #endregion

    #region 2.5D 가족 승객 정류장 인도 위 배치

    private static void SetupFamilyPassengers(Transform parent, Dictionary<string, Landmark> landmarks, TMP_FontAsset fontAsset)
    {
        GameObject passengersRoot = new GameObject("FamilyPassengers");
        passengersRoot.transform.SetParent(parent);

        var passengerList = new[]
        {
            new { Name = "선율이", SpawnLM = "아이파크캐슬", DestLM = "나라유치원", Tex = "선율.png", Offset = new Vector3(-3.5f, 0f, 3.5f) },
            new { Name = "태율이", SpawnLM = "아이파크캐슬", DestLM = "코코어린이집", Tex = "태율.jpg", Offset = new Vector3(0f, 0f, 4.0f) },
            new { Name = "엄마",   SpawnLM = "아이파크캐슬", DestLM = "하남 스타필드", Tex = "엄마.png", Offset = new Vector3(3.5f, 0f, 3.5f) },
            new { Name = "아빠",   SpawnLM = "아이파크캐슬", DestLM = "글빛공원", Tex = "아빠.png", Offset = new Vector3(6.5f, 0f, 3.5f) },
            new { Name = "친할머니", SpawnLM = "왕십리 삼성아파트", DestLM = "아이파크캐슬", Tex = "친할머니.png", Offset = new Vector3(-2f, 0f, 3.5f) },
            new { Name = "친할아버지", SpawnLM = "왕십리 삼성아파트", DestLM = "에버랜드", Tex = "친할아버지.png", Offset = new Vector3(2f, 0f, 3.5f) },
            new { Name = "외할머니", SpawnLM = "하남 풍산 아이파크", DestLM = "롯데월드", Tex = "외할머니.png", Offset = new Vector3(-2f, 0f, 3.5f) },
            new { Name = "외할아버지", SpawnLM = "하남 풍산 아이파크", DestLM = "인천공항", Tex = "외할아버지.png", Offset = new Vector3(2f, 0f, 3.5f) }
        };

        foreach (var p in passengerList)
        {
            if (!landmarks.ContainsKey(p.SpawnLM)) continue;
            Vector3 spawnPos = landmarks[p.SpawnLM].transform.position + p.Offset;
            CreatePassengerObject(passengersRoot.transform, p.Name, p.DestLM, p.Tex, spawnPos, fontAsset);
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

        SphereCollider col = pObj.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 2.8f;

        GameObject visualRoot = new GameObject("VisualRoot");
        visualRoot.transform.SetParent(pObj.transform);
        visualRoot.transform.localPosition = Vector3.zero;
        passenger.visualRoot = visualRoot.transform;

        // 초상화 Quad
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "PortraitQuad";
        quad.transform.SetParent(visualRoot.transform);
        quad.transform.localPosition = new Vector3(0f, 1.35f, 0f);
        quad.transform.localScale = new Vector3(1.8f, 1.8f, 1f);
        UnityEngine.Object.DestroyImmediate(quad.GetComponent<Collider>());

        string texPath = Path.Combine(FamilyTexturesDir, texFileName);
        Texture2D faceTex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default"));
        if (faceTex != null)
        {
            mat.mainTexture = faceTex;
            passenger.faceTexture = faceTex;
        }
        quad.GetComponent<MeshRenderer>().sharedMaterial = mat;
        passenger.portraitRenderer = quad.GetComponent<MeshRenderer>();

        // 머리 위 이름표 TMP
        GameObject nameObj = new GameObject("NameTagTMP");
        nameObj.transform.SetParent(visualRoot.transform);
        nameObj.transform.localPosition = new Vector3(0f, 2.65f, 0f);
        TextMeshPro nameTMP = nameObj.AddComponent<TextMeshPro>();
        nameTMP.text = $"{name}\n<size=70%><color=#FFE680>▶ {destination}</color></size>";
        nameTMP.fontSize = 4.8f;
        nameTMP.alignment = TextAlignmentOptions.Center;
        nameTMP.fontStyle = FontStyles.Bold;
        if (fontAsset != null) nameTMP.font = fontAsset;
        passenger.nameTagText = nameTMP;

        // 발밑 반짝이 파티클
        GameObject sparkleObj = new GameObject("SparkleParticle");
        sparkleObj.transform.SetParent(visualRoot.transform);
        sparkleObj.transform.localPosition = new Vector3(0f, 0.2f, 0f);
        ParticleSystem ps = sparkleObj.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.playOnAwake = true;
        main.startSize = 0.4f;
        main.startLifetime = 1.2f;
        main.startColor = new Color(1f, 0.9f, 0.3f, 0.85f);
        passenger.sparkleParticle = ps;
    }

    #endregion

    #region 상용 모바일 UI & 퀴즈 모달

    private static void SetupCommercialGradeUI(BusController bus, TMP_FontAsset fontAsset)
    {
        GameObject canvasObj = GameObject.Find("GameCanvas");
        if (canvasObj == null)
        {
            canvasObj = new GameObject("GameCanvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(2560, 1600); // 갤럭시 탭 S9 울트라 (16:10 해상도)
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();
        }

        GameHUD hud = canvasObj.GetComponent<GameHUD>() ?? canvasObj.AddComponent<GameHUD>();
        MobileTouchControls controls = canvasObj.GetComponent<MobileTouchControls>() ?? canvasObj.AddComponent<MobileTouchControls>();

        // 1. 상단 미션 안내 바 (둥근 글래스모피즘 테두리 바)
        SetupTopMissionHUD(canvasObj.transform, hud, fontAsset);

        // 2. 모바일 터치 페달 및 조향 버튼 (가속, 브레이크, 좌/우, 빵빵)
        SetupMobileTouchHUD(canvasObj.transform, hud, controls, fontAsset);

        // 3. 미니게임 퀴즈 모달 팝업
        SetupMiniGameModal(canvasObj.transform, fontAsset);
    }

    private static void SetupTopMissionHUD(Transform canvasTransform, GameHUD hud, TMP_FontAsset fontAsset)
    {
        Sprite panelSprite = AssetDatabase.LoadAssetAtPath<Sprite>(Path.Combine(UITexturesDir, "UI_Panel_Rounded.png"));

        // 상단 컨테이너
        GameObject topBar = new GameObject("TopMissionBar");
        topBar.transform.SetParent(canvasTransform, false);
        RectTransform rt = topBar.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.08f, 0.88f);
        rt.anchorMax = new Vector2(0.92f, 0.98f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        Image bg = topBar.AddComponent<Image>();
        if (panelSprite != null)
        {
            bg.sprite = panelSprite;
            bg.type = Image.Type.Sliced;
        }
        else
        {
            bg.color = new Color(0.12f, 0.16f, 0.28f, 0.9f);
        }

        // 별 아이콘
        GameObject starObj = new GameObject("StarIcon");
        starObj.transform.SetParent(topBar.transform, false);
        RectTransform starRT = starObj.AddComponent<RectTransform>();
        starRT.anchorMin = new Vector2(0.015f, 0.15f);
        starRT.anchorMax = new Vector2(0.065f, 0.85f);
        starRT.offsetMin = starRT.offsetMax = Vector2.zero;
        TextMeshProUGUI starTMP = starObj.AddComponent<TextMeshProUGUI>();
        starTMP.text = "⭐";
        starTMP.fontSize = 54;
        starTMP.alignment = TextAlignmentOptions.Center;

        // 미션 텍스트 (줄바꿈 없이 넉넉하고 시원한 텍스트)
        GameObject missionTextObj = new GameObject("MissionText");
        missionTextObj.transform.SetParent(topBar.transform, false);
        RectTransform textRT = missionTextObj.AddComponent<RectTransform>();
        textRT.anchorMin = new Vector2(0.07f, 0.1f);
        textRT.anchorMax = new Vector2(0.68f, 0.9f);
        textRT.offsetMin = textRT.offsetMax = Vector2.zero;

        TextMeshProUGUI missionTMP = missionTextObj.AddComponent<TextMeshProUGUI>();
        missionTMP.text = "선율이를 <color=#FFEB3B>[나라유치원]</color>에 데려다주세요! 🎒";
        missionTMP.fontSize = 44;
        missionTMP.fontStyle = FontStyles.Bold;
        missionTMP.color = Color.white;
        missionTMP.enableAutoSizing = true;
        missionTMP.fontSizeMin = 28;
        missionTMP.fontSizeMax = 48;
        missionTMP.alignment = TextAlignmentOptions.MidlineLeft;
        if (fontAsset != null) missionTMP.font = fontAsset;
        hud.missionTextTMP = missionTMP;

        // 배터리 & 속도 표시
        GameObject statusObj = new GameObject("StatusGroup");
        statusObj.transform.SetParent(topBar.transform, false);
        RectTransform statusRT = statusObj.AddComponent<RectTransform>();
        statusRT.anchorMin = new Vector2(0.70f, 0.1f);
        statusRT.anchorMax = new Vector2(0.98f, 0.9f);
        statusRT.offsetMin = statusRT.offsetMax = Vector2.zero;

        TextMeshProUGUI statusTMP = statusObj.AddComponent<TextMeshProUGUI>();
        statusTMP.text = "100% ⚡ | 0 km/h";
        statusTMP.fontSize = 40;
        statusTMP.fontStyle = FontStyles.Bold;
        statusTMP.color = new Color(0.4f, 1f, 0.55f);
        statusTMP.alignment = TextAlignmentOptions.MidlineRight;
        if (fontAsset != null) statusTMP.font = fontAsset;
        hud.batteryTextTMP = statusTMP;

        // 토스트 알림 패널
        GameObject toast = new GameObject("ToastPanel");
        toast.transform.SetParent(canvasTransform, false);
        RectTransform toastRT = toast.AddComponent<RectTransform>();
        toastRT.anchorMin = new Vector2(0.25f, 0.74f);
        toastRT.anchorMax = new Vector2(0.75f, 0.84f);
        toastRT.offsetMin = toastRT.offsetMax = Vector2.zero;

        Image toastBG = toast.AddComponent<Image>();
        if (panelSprite != null) { toastBG.sprite = panelSprite; toastBG.type = Image.Type.Sliced; toastBG.color = new Color(1f, 0.92f, 0.3f, 0.98f); }
        else toastBG.color = new Color(1f, 0.9f, 0.2f, 0.95f);

        GameObject toastTextObj = new GameObject("ToastText");
        toastTextObj.transform.SetParent(toast.transform, false);
        RectTransform ttRT = toastTextObj.AddComponent<RectTransform>();
        ttRT.anchorMin = Vector2.zero;
        ttRT.anchorMax = Vector2.one;
        ttRT.offsetMin = ttRT.offsetMax = Vector2.zero;

        TextMeshProUGUI toastTMP = toastTextObj.AddComponent<TextMeshProUGUI>();
        toastTMP.text = "선율이 탑승 완료! (1/4)";
        toastTMP.fontSize = 46;
        toastTMP.fontStyle = FontStyles.Bold;
        toastTMP.color = new Color(0.12f, 0.12f, 0.18f);
        toastTMP.alignment = TextAlignmentOptions.Center;
        if (fontAsset != null) toastTMP.font = fontAsset;

        hud.toastPanel = toast;
        hud.toastTextTMP = toastTMP;
        toast.SetActive(false);
    }

    private static void SetupMobileTouchHUD(Transform canvasTransform, GameHUD hud, MobileTouchControls controls, TMP_FontAsset fontAsset)
    {
        Sprite steerSprite = AssetDatabase.LoadAssetAtPath<Sprite>(Path.Combine(UITexturesDir, "UI_Btn_Steer.png"));
        Sprite gasSprite = AssetDatabase.LoadAssetAtPath<Sprite>(Path.Combine(UITexturesDir, "UI_Pedal_Gas.png"));
        Sprite brakeSprite = AssetDatabase.LoadAssetAtPath<Sprite>(Path.Combine(UITexturesDir, "UI_Pedal_Brake.png"));
        Sprite hornSprite = AssetDatabase.LoadAssetAtPath<Sprite>(Path.Combine(UITexturesDir, "UI_Btn_Horn.png"));

        // 좌측 핸들 조향 버튼 (좌/우)
        CreateSpriteTouchButton(canvasTransform, "Btn_Left", "◀", new Vector2(0.04f, 0.05f), new Vector2(0.14f, 0.22f), steerSprite, fontAsset,
            controls.OnLeftDown, controls.OnLeftUp);

        CreateSpriteTouchButton(canvasTransform, "Btn_Right", "▶", new Vector2(0.16f, 0.05f), new Vector2(0.26f, 0.22f), steerSprite, fontAsset,
            controls.OnRightDown, controls.OnRightUp);

        // 우측 페달 (가속 / 브레이크)
        CreateSpriteTouchButton(canvasTransform, "Btn_Gas", "가속\n🚀", new Vector2(0.85f, 0.05f), new Vector2(0.96f, 0.25f), gasSprite, fontAsset,
            controls.OnGasDown, controls.OnGasUp);

        CreateSpriteTouchButton(canvasTransform, "Btn_Brake", "정지\n🛑", new Vector2(0.72f, 0.05f), new Vector2(0.83f, 0.22f), brakeSprite, fontAsset,
            controls.OnBrakeDown, controls.OnBrakeUp);

        // 중앙 빵빵 경적 버튼
        GameObject hornObj = CreateSpriteTouchButton(canvasTransform, "Btn_Horn", "빵빵!\n📢", new Vector2(0.44f, 0.04f), new Vector2(0.56f, 0.20f), hornSprite, fontAsset,
            controls.OnHornClicked, null);

        hud.hornButton = hornObj.GetComponent<Button>();
    }

    private static GameObject CreateSpriteTouchButton(Transform parent, string name, string label, Vector2 anchorMin, Vector2 anchorMax, Sprite sprite, TMP_FontAsset fontAsset, Action onDown, Action onUp)
    {
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent, false);
        RectTransform rt = btnObj.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        Image img = btnObj.AddComponent<Image>();
        if (sprite != null) img.sprite = sprite;
        else img.color = new Color(0.2f, 0.6f, 1f, 0.85f);

        Button btn = btnObj.AddComponent<Button>();

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

        GameObject txtObj = new GameObject("Label");
        txtObj.transform.SetParent(btnObj.transform, false);
        RectTransform txtRT = txtObj.AddComponent<RectTransform>();
        txtRT.anchorMin = Vector2.zero;
        txtRT.anchorMax = Vector2.one;
        txtRT.offsetMin = txtRT.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = txtObj.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 42;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        if (fontAsset != null) tmp.font = fontAsset;

        return btnObj;
    }

    private static void SetupMiniGameModal(Transform canvasTransform, TMP_FontAsset fontAsset)
    {
        Sprite panelSprite = AssetDatabase.LoadAssetAtPath<Sprite>(Path.Combine(UITexturesDir, "UI_Panel_Rounded.png"));

        GameObject modalObj = new GameObject("MiniGameModal");
        modalObj.transform.SetParent(canvasTransform, false);
        RectTransform rt = modalObj.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        Image dimmer = modalObj.AddComponent<Image>();
        dimmer.color = new Color(0f, 0f, 0f, 0.78f);

        MiniGameUI miniGame = modalObj.AddComponent<MiniGameUI>();
        miniGame.modalRoot = modalObj;

        // 팝업 카드 상자
        GameObject cardBox = new GameObject("CardBox");
        cardBox.transform.SetParent(modalObj.transform, false);
        RectTransform boxRT = cardBox.AddComponent<RectTransform>();
        boxRT.anchorMin = new Vector2(0.18f, 0.16f);
        boxRT.anchorMax = new Vector2(0.82f, 0.84f);
        boxRT.offsetMin = boxRT.offsetMax = Vector2.zero;

        Image boxBG = cardBox.AddComponent<Image>();
        if (panelSprite != null) { boxBG.sprite = panelSprite; boxBG.type = Image.Type.Sliced; }
        else boxBG.color = new Color(0.98f, 0.98f, 0.94f);

        // 타이틀
        GameObject titleObj = new GameObject("ModalTitle");
        titleObj.transform.SetParent(cardBox.transform, false);
        RectTransform tRT = titleObj.AddComponent<RectTransform>();
        tRT.anchorMin = new Vector2(0.05f, 0.83f);
        tRT.anchorMax = new Vector2(0.95f, 0.96f);
        tRT.offsetMin = tRT.offsetMax = Vector2.zero;

        TextMeshProUGUI titleTMP = titleObj.AddComponent<TextMeshProUGUI>();
        titleTMP.text = "⭐ 신나는 도착 퀴즈! ⭐";
        titleTMP.fontSize = 58;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.color = new Color(1f, 0.88f, 0.2f);
        titleTMP.alignment = TextAlignmentOptions.Center;
        if (fontAsset != null) titleTMP.font = fontAsset;
        miniGame.titleTMP = titleTMP;

        // 질문
        GameObject qObj = new GameObject("ModalQuestion");
        qObj.transform.SetParent(cardBox.transform, false);
        RectTransform qRT = qObj.AddComponent<RectTransform>();
        qRT.anchorMin = new Vector2(0.05f, 0.66f);
        qRT.anchorMax = new Vector2(0.95f, 0.81f);
        qRT.offsetMin = qRT.offsetMax = Vector2.zero;

        TextMeshProUGUI qTMP = qObj.AddComponent<TextMeshProUGUI>();
        qTMP.text = "어디에 도착했을까요? 알맞은 간판을 찾아 터치해 볼까요?";
        qTMP.fontSize = 44;
        qTMP.color = Color.white;
        qTMP.alignment = TextAlignmentOptions.Center;
        if (fontAsset != null) qTMP.font = fontAsset;
        miniGame.questionTMP = qTMP;

        // 3개 선택 카드
        Color[] cardColors = {
            new Color(0.2f, 0.7f, 1f),
            new Color(1f, 0.65f, 0.15f),
            new Color(0.35f, 0.85f, 0.45f)
        };

        for (int i = 0; i < 3; i++)
        {
            float minX = 0.06f + i * 0.32f;
            float maxX = minX + 0.26f;

            GameObject card = new GameObject($"Card_{i}");
            card.transform.SetParent(cardBox.transform, false);
            RectTransform cRT = card.AddComponent<RectTransform>();
            cRT.anchorMin = new Vector2(minX, 0.22f);
            cRT.anchorMax = new Vector2(maxX, 0.58f);
            cRT.offsetMin = cRT.offsetMax = Vector2.zero;

            Image cImg = card.AddComponent<Image>();
            if (panelSprite != null) { cImg.sprite = panelSprite; cImg.type = Image.Type.Sliced; cImg.color = cardColors[i]; }
            else cImg.color = cardColors[i];

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
            cTMP.fontSize = 46;
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
        celTMP.fontSize = 50;
        celTMP.fontStyle = FontStyles.Bold;
        celTMP.color = new Color(1f, 0.9f, 0.25f);
        celTMP.alignment = TextAlignmentOptions.Center;
        if (fontAsset != null) celTMP.font = fontAsset;
        miniGame.celebrationTMP = celTMP;
        celObj.SetActive(false);

        modalObj.SetActive(false);
    }

    #endregion

    #region 매니저 & 유틸

    private static void SetupManagers(Transform parent)
    {
        GameObject mgrObj = GameObject.Find("PassengerManager");
        if (mgrObj == null)
        {
            mgrObj = new GameObject("PassengerManager");
            mgrObj.transform.SetParent(parent);
        }
        if (mgrObj.GetComponent<PassengerManager>() == null)
        {
            mgrObj.AddComponent<PassengerManager>();
        }
    }

    [MenuItem("CocoNara/2. 핑크색 머티리얼 URP로 일괄 변환 (Fix Pink Materials)", false, 2)]
    public static void ConvertAllLegacyMaterialsToURP()
    {
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null) return;

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
    }

    [MenuItem("CocoNara/3. EventSystem 입력 모듈 오류 수정 (Fix Input Error)", false, 3)]
    public static void FixEventSystemInputModule()
    {
        EventSystem[] allES = UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None);
        Type newModuleType = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");

        foreach (var es in allES)
        {
            StandaloneInputModule sim = es.GetComponent<StandaloneInputModule>();
            if (sim != null)
            {
                UnityEngine.Object.DestroyImmediate(sim);
            }

            if (newModuleType != null && es.GetComponent(newModuleType) == null)
            {
                es.gameObject.AddComponent(newModuleType);
            }
            EditorUtility.SetDirty(es.gameObject);
        }
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
            catch { }
        }
        return TMP_Settings.defaultFontAsset;
    }

    private static void GenerateBusFaceTextures()
    {
        // 텍스처 파일 확인
        string normalPath = Path.Combine(BusFacesDir, "BusFace_Normal.png");
        string winkPath = Path.Combine(BusFacesDir, "BusFace_Wink.png");
        string chargingPath = Path.Combine(BusFacesDir, "BusFace_Charging.png");

        if (!File.Exists(normalPath) || !File.Exists(winkPath) || !File.Exists(chargingPath))
        {
            AssetDatabase.Refresh();
        }
    }

    private static void EnsureFamilyMemberTextures()
    {
        string[] extraMembers = { "친할머니", "친할아버지", "외할머니", "외할아버지" };
        foreach (var m in extraMembers)
        {
            string path = Path.Combine(FamilyTexturesDir, m + ".png");
            if (!File.Exists(path))
            {
                AssetDatabase.Refresh();
                break;
            }
        }
    }

    #endregion
}

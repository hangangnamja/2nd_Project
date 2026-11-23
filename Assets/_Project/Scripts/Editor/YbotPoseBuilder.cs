using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Game.EditorTools
{
    // 에디터 상단 메뉴 Tools > Models > Ybot 동작 만들기:
    // 애니메이션이 없는 Ybot(휴머노이드)에 쓸 전투 동작 클립(대기·공격·시전·피격·쓰러짐)을 근육 값 키프레임으로 만든다.
    // 자세를 코드의 숫자로 정해 두어, 값을 고치고 메뉴를 다시 누르면 같은 에셋(GUID 유지)에 덮어쓴다.
    public static class YbotPoseBuilder
    {
        private const string ModelPath = "Assets/3Dmodels/Player/Y Bot.fbx";
        private const string WalkPath = "Assets/3Dmodels/Player/Walking (1).fbx";
        private const string IdlePath = "Assets/3Dmodels/Player/Idle.fbx";
        private const string IdleMainPath = "Assets/3Dmodels/Player/Idle_main.fbx";
        private const string PrayPath = "Assets/3Dmodels/Player/Praying.fbx";
        private const string BowPath = "Assets/3Dmodels/Player/Standing Aim Recoil.fbx";
        private const string SwordSlashPath = "Assets/3Dmodels/Player/Stable Sword Inward Slash.fbx";
        private const string AgonyPath = "Assets/3Dmodels/Player/Agony.fbx";
        private const string PowerUpPath = "Assets/3Dmodels/Player/Sword And Shield Power Up.fbx";

        // 걷기 애니메이션 재생 배율 (2.4배 → 초당 약 4.4 이동에 발걸음이 맞음).
        private const float WalkAnimSpeed = 2.4f;
        private const string Folder = "Assets/_Project/Art/Character/Ybot";
        private const string ControllerPath = Folder + "/YbotBattle.controller";

        // 근육 값 방향 (실제 Animator로 재서 확인한 값, 이름과 반대인 것이 있어 주의):
        // - Spine/Chest Front-Back: + = 뒤로 젖힘, - = 앞으로 숙임
        // - Head Nod Down-Up: + = 고개 들기 / Arm Down-Up: + = 팔 들기 / Arm Front-Back: + = 팔 뒤로, - = 앞으로
        // - Upper Leg Front-Back: 값이 작을수록 허벅지가 앞으로 (0.6 = 똑바로 섬) / Lower Leg Stretch: 1 = 무릎 펴짐
        // - Spine/Chest Twist Left-Right: + = 오른쪽으로 돌림 (모델은 +z를 보고, 오른쪽이 +x)

        // 자세 하나: 근육 이름 → 값 + 몸 전체 앞 기울기(도). 기울기는 RootQ(몸 중심 회전)로 넣는다.
        private class Pose
        {
            public readonly Dictionary<string, float> Muscles = new Dictionary<string, float>();
            public float LeanDegrees;
            // 땅에 닿는 기준 뼈에 무릎도 넣을지 (무릎 꿇은 자세는 무릎이 땅에 닿아야 해서).
            public bool KneesTouchGround;

            // 이 자세를 복사한 뒤 일부 근육만 바꾼 새 자세를 만든다 (기본 자세 위에 덧칠하는 용도).
            public Pose With(float lean, params (string muscle, float value)[] changes)
            {
                var pose = new Pose { LeanDegrees = lean, KneesTouchGround = KneesTouchGround };
                foreach (KeyValuePair<string, float> pair in Muscles)
                {
                    pose.Muscles[pair.Key] = pair.Value;
                }

                foreach ((string muscle, float value) in changes)
                {
                    pose.Muscles[muscle] = value;
                }

                return pose;
            }
        }

        [MenuItem("Tools/Models/Ybot 동작 만들기")]
        private static void Build()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.CreateFolder("Assets/_Project/Art/Character", "Ybot");
            }

            Pose stand = CreateStandPose();
            var clips = new Dictionary<string, AnimationClip>
            {
                ["Idle"] = WriteClip("YbotIdle", BuildIdleKeys(stand), loop: true),
                ["Attack"] = WriteClip("YbotAttack", BuildAttackKeys(stand), loop: false),
                ["Cast"] = WriteClip("YbotCast", BuildCastKeys(stand), loop: false),
                ["Hit"] = WriteClip("YbotHit", BuildHitKeys(stand), loop: false),
                ["Die"] = WriteClip("YbotDie", BuildDieKeys(stand), loop: false),
                // 스킬 전용 동작: 전용 연출(SkillFx)이 UnitView.PlayModelState(상태 이름)로 직접 재생한다.
                ["Slash"] = WriteClip("YbotSlash", BuildSlashKeys(stand), loop: false),
            };

            // 합장은 Mixamo Praying의 한 프레임을 굳혀 쓴다 (원본의 몸 흔들림 없이 0.35초 만에 합장하고 멈춤).
            AnimationClip gassho = WriteFrozenPrayClip(0.35f);
            if (gassho != null)
            {
                clips["Gassho"] = gassho;
            }

            // 걷기·대기는 직접 만들지 않고 Mixamo 애니메이션을 그대로 쓴다 (반복·제자리는 FBX 가져오기 설정에서 정해 둠).
            //  - Walk: 대상에게 걸어갈 때 (UnitModel 스킬 동작 "Walk")
            //  - Idle: 평소 대기 (FBX가 없으면 위에서 만든 YbotIdle 그대로)
            //  - IdleMain: 자기 차례(스킬을 쓰는 턴)의 대기 (UnitModel.readyIdleState)
            AddFbxClip(clips, "Walk", WalkPath);
            AddFbxClip(clips, "Idle", IdlePath);
            AddFbxClip(clips, "IdleMain", IdleMainPath);
            //  - Bow: 활쏘기 (흉신 마라 업화). 다 당긴 자세 → 0.17초쯤 놓음 → 반동. HellfireBowFx가 재생 속도를 연출에 맞춰 조절한다
            AddFbxClip(clips, "Bow", BowPath);
            //  - SwordSlash: 칼 베기 (흉신 마라 영역 공격 전 클로즈업). 크게 휘두르는 순간 약 1.17초, 끝나면 Idle로
            AddFbxClip(clips, "SwordSlash", SwordSlashPath);
            //  - Agony: 머리를 감싸 쥐고 괴로워함 (하이퍼캐리 변신 중 구체 안, 반복)
            //  - PowerUp: 힘을 끌어올리는 자세 (하이퍼캐리 변신 구체가 깨질 때, 끝나면 Idle로)
            AddFbxClip(clips, "Agony", AgonyPath);
            AddFbxClip(clips, "PowerUp", PowerUpPath);

            BuildController(clips);
            AssetDatabase.SaveAssets();
            Debug.Log("[Ybot] 전투 동작 클립 5개와 컨트롤러를 만들었습니다.");
        }

        // 기본 서 있는 자세: 무릎을 살짝 굽히고 팔은 몸 옆에 늘어뜨림. 다른 자세는 모두 이 자세에서 일부만 바꾼다.
        private static Pose CreateStandPose()
        {
            var pose = new Pose();
            SetBoth(pose, "Upper Leg Front-Back", 0.5f);
            SetBoth(pose, "Lower Leg Stretch", 0.85f);
            SetBoth(pose, "Upper Leg In-Out", 0.08f);
            SetBoth(pose, "Arm Down-Up", -0.55f);
            SetBoth(pose, "Arm Front-Back", -0.05f);
            SetBoth(pose, "Forearm Stretch", 0.7f);
            pose.Muscles["Spine Front-Back"] = -0.05f;
            pose.Muscles["Head Nod Down-Up"] = -0.05f;
            return pose;
        }

        // 대기 (2.4초 반복): 숨 쉬듯 상체·팔이 아주 조금 오르내림. 처음과 끝 자세가 같아야 반복할 때 튀지 않는다.
        private static List<(float time, Pose pose)> BuildIdleKeys(Pose stand)
        {
            Pose inhale = stand.With(0f,
                ("Spine Front-Back", 0.0f),
                ("Left Arm Down-Up", -0.52f), ("Right Arm Down-Up", -0.52f),
                ("Left Lower Leg Stretch", 0.9f), ("Right Lower Leg Stretch", 0.9f));
            return new List<(float, Pose)> { (0f, stand), (1.2f, inhale), (2.4f, stand) };
        }

        // 공격 (오른손 지르기, 0.85초): 몸을 오른쪽으로 감아 주먹을 뒤로 뺐다가(0.18초) 왼발을 내딛으며 앞으로 뻗음(0.38초 = 타격).
        // UnitModel의 타격 시간(attackImpactSeconds)을 0.38로 맞춘다.
        private static List<(float time, Pose pose)> BuildAttackKeys(Pose stand)
        {
            Pose windup = stand.With(-4f,
                ("Spine Twist Left-Right", 0.4f), ("Chest Twist Left-Right", 0.3f),
                ("Right Arm Front-Back", 0.5f), ("Right Arm Down-Up", -0.3f), ("Right Forearm Stretch", 0.15f),
                ("Left Arm Front-Back", -0.4f), ("Left Arm Down-Up", -0.2f), ("Left Forearm Stretch", 0.4f),
                ("Left Upper Leg Front-Back", 0.35f), ("Right Upper Leg Front-Back", 0.7f));
            Pose strike = stand.With(12f,
                ("Spine Twist Left-Right", -0.45f), ("Chest Twist Left-Right", -0.4f), ("Spine Front-Back", -0.15f),
                ("Right Arm Front-Back", -0.95f), ("Right Arm Down-Up", 0.05f), ("Right Forearm Stretch", 1f),
                ("Left Arm Front-Back", 0.35f), ("Left Arm Down-Up", -0.35f), ("Left Forearm Stretch", 0.3f),
                ("Left Upper Leg Front-Back", 0.2f), ("Left Lower Leg Stretch", 0.75f),
                ("Right Upper Leg Front-Back", 0.85f), ("Right Lower Leg Stretch", 1f));
            return new List<(float, Pose)> { (0f, stand), (0.18f, windup), (0.38f, strike), (0.52f, strike), (0.85f, stand) };
        }

        // 시전 (1.1초): 두 팔을 앞 위로 들어 올리며 가슴을 펴고 고개를 듦. 가장 높이 든 순간(0.35초)부터 잠깐 버팀.
        private static List<(float time, Pose pose)> BuildCastKeys(Pose stand)
        {
            Pose raise = stand.With(-3f,
                ("Left Arm Down-Up", 0.45f), ("Right Arm Down-Up", 0.45f),
                ("Left Arm Front-Back", -0.6f), ("Right Arm Front-Back", -0.6f),
                ("Left Forearm Stretch", 0.9f), ("Right Forearm Stretch", 0.9f),
                ("Chest Front-Back", 0.2f), ("Head Nod Down-Up", 0.25f),
                ("Left Lower Leg Stretch", 1f), ("Right Lower Leg Stretch", 1f));
            return new List<(float, Pose)> { (0f, stand), (0.35f, raise), (0.75f, raise), (1.1f, stand) };
        }

        // 피격 (0.5초): 맞는 순간(0.08초) 상체가 뒤로 젖혀지고 팔이 벌어졌다가 돌아옴.
        private static List<(float time, Pose pose)> BuildHitKeys(Pose stand)
        {
            Pose recoil = stand.With(-8f,
                ("Spine Front-Back", 0.3f), ("Chest Front-Back", 0.2f), ("Head Nod Down-Up", 0.3f),
                ("Left Arm Front-Back", 0.3f), ("Right Arm Front-Back", 0.3f),
                ("Left Arm Down-Up", -0.35f), ("Right Arm Down-Up", -0.35f),
                ("Left Forearm Stretch", 0.5f), ("Right Forearm Stretch", 0.5f));
            return new List<(float, Pose)> { (0f, stand), (0.08f, recoil), (0.5f, stand) };
        }

        // 쓰러짐 (1.2초, 마지막 자세 유지): 뒤로 휘청한 뒤 무릎이 꺾이며 주저앉고 상체·고개가 앞으로 떨어짐.
        // 무릎 꿇은 자세는 무릎이 땅에 닿도록 KneesTouchGround를 켠다.
        private static List<(float time, Pose pose)> BuildDieKeys(Pose stand)
        {
            Pose stagger = stand.With(-6f,
                ("Spine Front-Back", 0.25f), ("Head Nod Down-Up", 0.3f),
                ("Left Arm Down-Up", -0.4f), ("Right Arm Down-Up", -0.4f));
            Pose buckle = stand.With(15f,
                ("Left Upper Leg Front-Back", 0.1f), ("Right Upper Leg Front-Back", 0.1f),
                ("Left Lower Leg Stretch", -0.3f), ("Right Lower Leg Stretch", -0.3f),
                ("Spine Front-Back", -0.4f), ("Chest Front-Back", -0.3f), ("Head Nod Down-Up", -0.5f),
                ("Left Arm Down-Up", -0.75f), ("Right Arm Down-Up", -0.75f));
            buckle.KneesTouchGround = true;
            Pose slump = buckle.With(25f,
                ("Left Upper Leg Front-Back", 0.0f), ("Right Upper Leg Front-Back", 0.0f),
                ("Left Lower Leg Stretch", -0.9f), ("Right Lower Leg Stretch", -0.9f),
                ("Spine Front-Back", -0.6f), ("Chest Front-Back", -0.45f), ("Head Nod Down-Up", -0.8f),
                ("Left Arm Front-Back", -0.25f), ("Right Arm Front-Back", -0.25f), ("Left Forearm Stretch", 0.5f));
            return new List<(float, Pose)> { (0f, stand), (0.2f, stagger), (0.65f, buckle), (1.2f, slump) };
        }

        // 팔 휘둘러 베기 (흉신 마라 2스, 1.8초): 오른팔을 머리 뒤로 감아 올렸다가(0.2초) 몸을 왼쪽으로 비틀며 가슴 앞을 가로질러 휘두름(0.4초 = 손이 닿는 순간).
        // InvertCleaveFx가 다가가기 시작할 때 재생하고, 화면 반전 동안 휘두른 자세로 버틴 뒤(1.3초까지) 선 자세로 돌아온다.
        // 팔 끝 위치는 실제 Animator로 재서 찾은 값: 감아 올림 = 손이 오른쪽 어깨 뒤 위, 휘두름 끝 = 손이 가슴 앞 왼쪽.
        private static List<(float time, Pose pose)> BuildSlashKeys(Pose stand)
        {
            Pose windup = stand.With(-3f,
                ("Spine Twist Left-Right", 0.45f), ("Chest Twist Left-Right", 0.45f),
                ("Right Arm Down-Up", 1f), ("Right Arm Front-Back", 0.8f), ("Right Forearm Stretch", -0.2f), ("Right Arm Twist In-Out", -0.6f),
                ("Left Arm Front-Back", -0.3f), ("Left Arm Down-Up", -0.4f), ("Left Forearm Stretch", 0.5f),
                ("Left Upper Leg Front-Back", 0.35f), ("Right Upper Leg Front-Back", 0.65f));
            Pose sweep = stand.With(10f,
                ("Spine Twist Left-Right", -0.5f), ("Chest Twist Left-Right", -0.5f), ("Spine Front-Back", -0.1f),
                ("Right Arm Down-Up", -0.5f), ("Right Arm Front-Back", -0.8f), ("Right Forearm Stretch", 1f),
                ("Right Shoulder Front-Back", -0.5f), ("Right Arm Twist In-Out", 0f),
                ("Left Arm Front-Back", 0.4f), ("Left Arm Down-Up", -0.5f), ("Left Forearm Stretch", 0.4f),
                ("Left Upper Leg Front-Back", 0.25f), ("Left Lower Leg Stretch", 0.8f),
                ("Right Upper Leg Front-Back", 0.8f), ("Right Lower Leg Stretch", 1f));
            return new List<(float, Pose)> { (0f, stand), (0.2f, windup), (0.4f, sweep), (1.3f, sweep), (1.8f, stand) };
        }

        // 합장 (흉신 마라 3스 영역 전개): Mixamo Praying 애니메이션의 한 프레임을 굳힌 클립을 만든다.
        // 원본은 합장한 채 몸을 앞뒤로 흔드는데, 흔들림을 없애려고 그 순간의 자세(모든 근육·Root 값)를 상수로 고정한다.
        // 대기(Idle.fbx) 첫 프레임 → blendSeconds 동안 합장 자세로 옮겨 간 뒤 그대로 머문다
        // (컨트롤러에서 Idle로 돌아가는 연결이 없고, 연출이 끝날 때 DomainDeployFx가 Idle로 되돌린다).
        private static AnimationClip WriteFrozenPrayClip(float blendSeconds)
        {
            AnimationClip pray = FindFbxClip(PrayPath);
            AnimationClip idle = FindFbxClip(IdlePath);
            if (pray == null)
            {
                return null;
            }

            string path = $"{Folder}/YbotGassho.anim";
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
            {
                clip = new AnimationClip();
                AssetDatabase.CreateAsset(clip, path);
            }

            clip.ClearCurves();

            // AnimationUtility.GetCurveBindings: 클립에 든 곡선 목록 (휴머노이드면 근육·RootT/Q·손발 목표 위치).
            // 곡선마다 합장 순간 값(Evaluate(시각))을 꺼내 "대기 값 → 합장 값 → 합장 값" 세 키로 다시 쓴다.
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(pray))
            {
                float target = AnimationUtility.GetEditorCurve(pray, binding).Evaluate(PraySampleSeconds);
                AnimationCurve idleCurve = idle != null ? AnimationUtility.GetEditorCurve(idle, binding) : null;
                float start = idleCurve != null ? idleCurve.Evaluate(0f) : target;

                var curve = new AnimationCurve(
                    new Keyframe(0f, start), new Keyframe(blendSeconds, target), new Keyframe(blendSeconds + 0.25f, target));
                AnimationUtility.SetEditorCurve(clip, binding, curve);
            }

            // 다른 클립과 같이 몸 중심 움직임을 자세에 넣는다 (아니면 루트 모션으로 버려져 높이가 어긋남).
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = false;
            settings.loopBlendPositionY = true;
            settings.keepOriginalPositionY = true;
            settings.loopBlendPositionXZ = true;
            settings.keepOriginalPositionXZ = true;
            settings.loopBlendOrientation = true;
            settings.keepOriginalOrientation = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        // 합장을 굳힐 Praying 애니메이션의 시각(초). 0초 = 흔들기 전 똑바로 선 채 가슴 앞에서 두 손을 맞댄 순간.
        private const float PraySampleSeconds = 0f;

        // 왼쪽·오른쪽 같은 근육에 같은 값을 넣는다 (Unity 근육은 좌우 대칭이라 같은 값 = 같은 동작).
        private static void SetBoth(Pose pose, string muscle, float value)
        {
            pose.Muscles["Left " + muscle] = value;
            pose.Muscles["Right " + muscle] = value;
        }

        // 키프레임 목록으로 클립을 만들거나 덮어쓴다. 기존 에셋은 지우지 않고 곡선만 바꿔 GUID(컨트롤러 연결)를 유지한다.
        private static AnimationClip WriteClip(string name, List<(float time, Pose pose)> keys, bool loop)
        {
            string path = $"{Folder}/{name}.anim";
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
            {
                clip = new AnimationClip();
                AssetDatabase.CreateAsset(clip, path);
            }

            clip.ClearCurves();
            WriteMuscleCurves(clip, keys);
            WriteRootCurves(clip, keys);

            // AnimationClipSettings.loopTime: 끝까지 재생하면 처음부터 다시 (대기만 반복).
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            // Bake Into Pose(위치 y·xz·회전): 몸 중심의 움직임을 "자세"로 넣는다.
            // 끄면 Unity가 RootT·RootQ 변화를 이동(루트 모션)으로 보고 버려서, 주저앉아도 몸이 내려가지 않는다.
            // keepOriginal…: 처음 키 기준이 아니라 클립에 적은 값 그대로 쓴다 (서 있는 높이 = 땅에 맞춘 값).
            settings.loopBlendPositionY = true;
            settings.keepOriginalPositionY = true;
            settings.loopBlendPositionXZ = true;
            settings.keepOriginalPositionXZ = true;
            settings.loopBlendOrientation = true;
            settings.keepOriginalOrientation = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        // 키프레임에 쓰인 모든 근육마다 곡선 하나. 어떤 키에 없는 근육은 0(휴머노이드 기본값)으로 본다.
        // 근육 이름이 틀리면 Animator가 조용히 무시하므로, HumanTrait.MuscleName(실제 근육 이름 목록)에 없으면 경고한다.
        private static void WriteMuscleCurves(AnimationClip clip, List<(float time, Pose pose)> keys)
        {
            var validNames = new HashSet<string>(HumanTrait.MuscleName);
            var used = new HashSet<string>();
            foreach ((float _, Pose pose) in keys)
            {
                used.UnionWith(pose.Muscles.Keys);
            }

            foreach (string muscle in used)
            {
                if (!validNames.Contains(muscle))
                {
                    Debug.LogWarning($"[Ybot] 없는 근육 이름: {muscle}");
                    continue;
                }

                var curve = new AnimationCurve();
                foreach ((float time, Pose pose) in keys)
                {
                    pose.Muscles.TryGetValue(muscle, out float value);
                    curve.AddKey(time, value);
                }

                SetCurve(clip, muscle, curve);
            }
        }

        // 몸 중심 위치·회전(RootT·RootQ) 곡선. 키마다 실제로 재서 발(무릎 꿇은 자세는 무릎)이 땅(y = 0)에 닿고 두 발 가운데가 원점에 오도록 맞춘다.
        // 이유: 근육만 바꾸면 몸 중심은 그대로라, 무릎을 굽히거나 숙이면 발이 공중에 뜨거나 땅에 박힌다.
        private static void WriteRootCurves(AnimationClip clip, List<(float time, Pose pose)> keys)
        {
            string[] names = { "RootT.x", "RootT.y", "RootT.z", "RootQ.x", "RootQ.y", "RootQ.z", "RootQ.w" };
            var curves = new AnimationCurve[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                curves[i] = new AnimationCurve();
            }

            GameObject model = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath));
            try
            {
                Animator animator = model.GetComponent<Animator>();
                // AlwaysAnimate: 카메라에 안 보여도 뼈를 움직임 (에디터에서 재는 중이라 기본값이면 갱신이 생략됨).
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                foreach ((float time, Pose pose) in keys)
                {
                    // Quaternion.Euler(기울기, 0, 0): x축으로 돌려 몸 위쪽을 앞(+z)으로 숙인다.
                    Quaternion rotation = Quaternion.Euler(pose.LeanDegrees, 0f, 0f);
                    Vector3 root = MeasureGroundedRoot(animator, pose, rotation);
                    float[] values = { root.x, root.y, root.z, rotation.x, rotation.y, rotation.z, rotation.w };
                    for (int i = 0; i < names.Length; i++)
                    {
                        curves[i].AddKey(time, values[i]);
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(model);
            }

            for (int i = 0; i < names.Length; i++)
            {
                SetCurve(clip, names[i], curves[i]);
            }
        }

        // 자세를 한 번 재생해 보고, 땅에 닿을 뼈의 가장 낮은 높이와 두 발 가운데 위치만큼 몸 중심을 옮긴다.
        // RootT는 몸 크기(humanScale)로 나눈 값이라, 옮길 거리 ÷ humanScale을 더한다. 한 번에 딱 맞지 않을 수 있어 두 번 반복한다.
        private static Vector3 MeasureGroundedRoot(Animator animator, Pose pose, Quaternion rotation)
        {
            Vector3 root = new Vector3(0f, 1f, 0f);
            for (int pass = 0; pass < 2; pass++)
            {
                SamplePose(animator, pose, root, rotation);
                Vector3 leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot).position;
                Vector3 rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot).position;
                float lowest = Mathf.Min(LowestPoint(animator, HumanBodyBones.LeftToes), LowestPoint(animator, HumanBodyBones.RightToes),
                    leftFoot.y - AnkleHeight, rightFoot.y - AnkleHeight);
                if (pose.KneesTouchGround)
                {
                    lowest = Mathf.Min(lowest,
                        animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg).position.y - KneeRadius,
                        animator.GetBoneTransform(HumanBodyBones.RightLowerLeg).position.y - KneeRadius);
                }

                // 기준 가운데: 서 있으면 두 발, 무릎 꿇으면 두 무릎 (꿇을 때 발은 뒤로 빠지므로 발 기준이면 몸이 앞으로 미끄러짐).
                Vector3 center = pose.KneesTouchGround
                    ? (animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg).position + animator.GetBoneTransform(HumanBodyBones.RightLowerLeg).position) * 0.5f
                    : (leftFoot + rightFoot) * 0.5f;
                root -= new Vector3(center.x, lowest, center.z) / animator.humanScale;
            }

            return root;
        }

        // 발목 뼈는 발바닥보다 이만큼 위 · 발가락 뼈 아래 두께 · 무릎 뼈 앞 두께 (Ybot 원본 T자세에서 발목 0.10, 발가락 0.03 높이).
        private const float AnkleHeight = 0.1f;
        private const float ToeThickness = 0.03f;
        private const float KneeRadius = 0.06f;

        private static float LowestPoint(Animator animator, HumanBodyBones toes) =>
            animator.GetBoneTransform(toes).position.y - ToeThickness;

        // 자세 하나를 상수 클립으로 만들어 실제 Animator로 재생한다 (게임과 같은 경로라 잰 뼈 위치가 게임과 같음).
        // 메모리에만 만든 상태 1개짜리 컨트롤러에 클립을 끼우고 Rebind(처음 상태로) → Update(0)(지금 자세 계산)로 뼈를 움직인다.
        // PlayableGraph로 바로 재생하는 방법은 에디터에서 뼈 위치가 어긋나게 나와서 쓰지 않는다.
        private static void SamplePose(Animator animator, Pose pose, Vector3 root, Quaternion rotation)
        {
            var clip = new AnimationClip();
            foreach (KeyValuePair<string, float> pair in pose.Muscles)
            {
                SetCurve(clip, pair.Key, AnimationCurve.Constant(0f, 1f, pair.Value));
            }

            float[] values = { root.x, root.y, root.z, rotation.x, rotation.y, rotation.z, rotation.w };
            string[] names = { "RootT.x", "RootT.y", "RootT.z", "RootQ.x", "RootQ.y", "RootQ.z", "RootQ.w" };
            for (int i = 0; i < names.Length; i++)
            {
                SetCurve(clip, names[i], AnimationCurve.Constant(0f, 1f, values[i]));
            }

            // 컨트롤러를 떼면 뼈가 원래 T자세로 돌아가므로, 뼈 위치를 다 읽기 전에는 떼지 않는다 (다음 측정 때 새 컨트롤러로 바꿔 끼움).
            var controller = new AnimatorController();
            controller.AddLayer("Base");
            controller.layers[0].stateMachine.AddState("Pose").motion = clip;
            animator.runtimeAnimatorController = controller;
            animator.Rebind();
            animator.Update(0f);
        }

        // FBX 안의 애니메이션 클립을 찾아 그 상태 이름으로 넣는다. 못 찾으면 넣지 않는다 (그 상태는 이전 클립 그대로이거나 없음).
        private static void AddFbxClip(Dictionary<string, AnimationClip> clips, string state, string fbxPath)
        {
            AnimationClip clip = FindFbxClip(fbxPath);
            if (clip != null)
            {
                clips[state] = clip;
            }
        }

        // FBX 안의 애니메이션 클립. FBX에는 미리보기용 "__preview__" 클립도 들어 있어 그것은 건너뛴다. 없으면 경고 후 null.
        private static AnimationClip FindFbxClip(string fbxPath)
        {
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
            {
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                {
                    return clip;
                }
            }

            Debug.LogWarning($"[Ybot] 애니메이션 클립을 찾지 못했습니다: {fbxPath}");
            return null;
        }

        // 휴머노이드 클립 곡선: 경로 ""(모델 맨 위) + Animator 형식 + 근육·Root 이름으로 저장한다.
        private static void SetCurve(AnimationClip clip, string property, AnimationCurve curve)
        {
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), property), curve);
        }

        // 동작 상태 + 걷기·차례 대기(Idle 기본)짜리 컨트롤러. 이미 있으면 상태의 클립만 바꿔 끼워 GUID(프리팹 연결)를 유지한다.
        // 공격·시전·피격·베기·칼 베기·파워업은 끝나면(exit time 1) Idle로 돌아가고 (없는 상태는 건너뜀), 쓰러짐·합장은 마지막 자세에 머문다. 재생은 UnitModel이 상태 이름으로 바로 한다.
        private static void BuildController(Dictionary<string, AnimationClip> clips)
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath)
                ?? AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;

            var states = new Dictionary<string, AnimatorState>();
            foreach (ChildAnimatorState child in machine.states)
            {
                states[child.state.name] = child.state;
            }

            foreach (KeyValuePair<string, AnimationClip> pair in clips)
            {
                if (!states.TryGetValue(pair.Key, out AnimatorState state))
                {
                    state = machine.AddState(pair.Key);
                    states[pair.Key] = state;
                }

                state.motion = pair.Value;
            }

            machine.defaultState = states["Idle"];

            // 걷기 상태 재생 속도: 대상에게 빨리 다가가도록 걷기 애니메이션을 빠르게 튼다 (AnimatorState.speed = 이 상태에만 곱해지는 속도).
            // 발이 미끄러지지 않으려면 UnitModel 스킬 동작 "Walk"의 걷는 속도 = 원본 걷기 속도(약 1.85) × 이 값으로 맞춘다.
            if (states.TryGetValue("Walk", out AnimatorState walkState))
            {
                walkState.speed = WalkAnimSpeed;
            }
            foreach (string name in new[] { "Attack", "Cast", "Hit", "Slash", "SwordSlash", "PowerUp" })
            {
                if (!states.TryGetValue(name, out AnimatorState state))
                {
                    continue;
                }

                if (state.transitions.Length == 0)
                {
                    AnimatorStateTransition back = state.AddTransition(states["Idle"]);
                    back.hasExitTime = true;
                    back.exitTime = 1f;
                    back.duration = 0.1f;
                }
            }

            EditorUtility.SetDirty(controller);
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using Game.Battle;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.UI
{
    // 달빛 일격 연출 (강탈 딜러 3스, 임시). 밤이 되며 안개 속으로 사라진 딜러가, 달을 등지고 적에게 내리꽂힌다.
    //  1) 밤·안개: 조명·주변광이 짙은 남색 밤빛으로 바뀌고 안개가 짙게 퍼짐. 딜러(옆 시점)는 안개 뭉치에 휩싸여 스르륵 사라짐
    //  2) 적 시점: 대상 뒤 낮은 곳에서 올려다보는 시점으로 컷 → 하늘의 커다란 달을 등지고 딜러가 공중에 나타나 잠깐 멈췄다가 내리꽂히기 시작
    //     (안개는 다시 옅어져 내려오는 모습이 보임. 달은 안개를 받지 않는 재질이라 늘 선명함)
    //  3) 위 시점: 내려찍기 직전에 위에서 내려다보는 시점으로 컷 → 딜러가 대상에게 내리꽂히는 순간 펑:
    //     큰 푸른 폭발·섬광 + 바닥에 퍼지는 충격파 고리 두 겹 + 얼음빛 균열 + 피해(onImpact 비율 1)·아주 강한 흔들림
    //  4) 여운 뒤 밤과 안개가 걷히고 원래 시점으로 (컷하는 순간 딜러는 원래 자리로)
    // 달·안개·폭발 모양은 그림 판·파티클이 맡고, 이 코드는 위치·순서·시간·카메라 컷·조명 값만 정한다.
    public class MoonStrikeFx : SkillFx
    {
        [Header("프리팹 부품")]
        [Tooltip("하늘에 뜨는 달 (자식 Quad, 안개를 받지 않는 달 그림 재질). 처음에는 꺼 둔다")]
        [SerializeField] private Renderer moon;

        [Tooltip("딜러가 사라지고 나타날 때 퍼지는 안개 뭉치 (끝나면 스스로 지워지는 프리팹)")]
        [SerializeField] private GameObject fogPuffPrefab;

        [Tooltip("내려찍을 때 터지는 충격파·폭발 (끝나면 스스로 지워지는 프리팹)")]
        [SerializeField] private GameObject impactPrefab;

        [Tooltip("내려찍는 순간 대상 자리에서 한 번 더 터지는 섬광 (비우면 생략)")]
        [SerializeField] private GameObject flashPrefab;

        [Tooltip("내려오는 동안 딜러 몸을 따라다니는 빛 (자식 파티클, 비우면 생략)")]
        [SerializeField] private ParticleSystem trail;

        [Tooltip("바닥 충격파·균열 견본 (꺼 둔 자식 LineRenderer, 바닥에 눕힘: Alignment = Transform Z, x축 90도)")]
        [SerializeField] private LineRenderer groundLine;

        [Header("1) 밤·안개·사라짐")]
        [Tooltip("밤이 내리는 시간(초) · 밤이 걷히는 시간(초)")]
        [SerializeField, Min(0.01f)] private float nightFadeSeconds = 0.5f;
        [SerializeField, Min(0.01f)] private float dayFadeSeconds = 0.35f;

        [Tooltip("밤의 해(주 조명) 밝기 배율 · 색")]
        [SerializeField, Range(0f, 1f)] private float nightLightScale = 0.35f;
        [SerializeField] private Color nightLightColor = new Color(0.6f, 0.72f, 1f);

        [Tooltip("밤의 주변광 · 안개·배경색")]
        [SerializeField] private Color nightAmbient = new Color(0.08f, 0.1f, 0.2f);
        [SerializeField] private Color nightSky = new Color(0.03f, 0.04f, 0.09f);

        [Tooltip("짙은 안개: 안개가 시작되는 거리 · 완전히 가려지는 거리 (원래 값보다 가깝게 → 짙어짐)")]
        [SerializeField, Min(0f)] private float thickFogStart = 2f;
        [SerializeField, Min(0.1f)] private float thickFogEnd = 16f;

        [Tooltip("사라지는 장면 길이(초) · 그중 딜러가 안개에 휩싸여 사라지는 시각(초)")]
        [SerializeField, Min(0.1f)] private float vanishShotSeconds = 1f;
        [SerializeField, Min(0f)] private float vanishAt = 0.45f;

        [Tooltip("사라지는 장면 카메라: 딜러 옆으로 떨어진 거리 · 높이(캡슐 중심 기준) · 화각")]
        [SerializeField, Min(1f)] private float vanishViewDistance = 6.5f;
        [SerializeField] private float vanishViewHeight = 0.6f;
        [SerializeField] private float vanishFieldOfView = 45f;

        [SerializeField, Min(0.1f)] private float puffScale = 1f;

        [Header("2) 적 시점 · 달")]
        [Tooltip("적 시점 카메라: 대상 뒤로 · 옆으로 · 위로 떨어진 자리, 화각")]
        [SerializeField] private float enemyViewBack = 2.2f;
        [SerializeField] private float enemyViewSide = 1.2f;
        [SerializeField] private float enemyViewHeight = -0.3f;
        [SerializeField] private float enemyViewFieldOfView = 60f;

        [Tooltip("딜러가 나타나는 정점: 높이 (캡슐 중심 기준) · 원래 자리 ~ 대상 사이 어디쯤 (0 = 원래 자리, 1 = 대상 위)")]
        [SerializeField, Min(0.5f)] private float apexHeight = 7f;
        [SerializeField, Range(0f, 1f)] private float apexForward = 0.5f;

        [Tooltip("정점에 나타나 머무는 시간(초). 적 시점에서는 안개를 이만큼 원래대로 옅게 걷어 낸다")]
        [SerializeField, Min(0f)] private float hangSeconds = 0.9f;

        [Tooltip("달: 카메라에서 정점 너머로 떨어진 거리 · 지름 · 밝기")]
        [SerializeField, Min(1f)] private float moonDistance = 24f;
        [SerializeField, Min(0.5f)] private float moonSize = 15f;
        [SerializeField] private float moonIntensity = 1.6f;

        [Header("3) 내려찍기 · 위 시점 · 펑")]
        [Tooltip("내리꽂히는 전체 시간(초) · 위 시점으로 넘어가는 진행도 (0~1, 내려찍기 직전)")]
        [SerializeField, Min(0.05f)] private float diveSeconds = 0.36f;
        [SerializeField, Range(0.3f, 0.98f)] private float topCutAt = 0.8f;

        [Tooltip("위 시점 카메라: 착지점 위 높이 · 딜러 쪽으로 물러나는 거리 · 화각")]
        [SerializeField, Min(2f)] private float topViewHeight = 13f;
        [SerializeField] private float topViewBack = 3f;
        [SerializeField] private float topViewFieldOfView = 55f;

        [Tooltip("대상 바로 앞(딜러 쪽)으로 떨어지는 거리 (대상과 겹치지 않게)")]
        [SerializeField, Min(0f)] private float landShort = 0.8f;

        [SerializeField, Min(0.1f)] private float impactScale = 2.6f;
        [SerializeField, Min(0.1f)] private float flashScale = 3f;
        [SerializeField, Min(0f)] private float impactShake = 1.3f;
        [SerializeField, Min(0f)] private float impactShakeSeconds = 0.8f;

        [Tooltip("충격파 고리가 퍼지는 최대 반지름 · 시간(초) · 두 번째 고리가 늦게 출발하는 시간(초)")]
        [SerializeField, Min(0.5f)] private float ringRadius = 7f;
        [SerializeField, Min(0.05f)] private float ringSeconds = 0.55f;
        [SerializeField, Min(0f)] private float secondRingDelay = 0.12f;

        [Tooltip("사방으로 갈라지는 균열 수 · 길이 · 뻗는 시간(초) · 남아 있는 시간(초)")]
        [SerializeField, Range(0, 16)] private int crackCount = 9;
        [SerializeField, Min(0f)] private float crackLength = 4.5f;
        [SerializeField, Min(0.01f)] private float crackSeconds = 0.18f;
        [SerializeField, Min(0f)] private float crackLingerSeconds = 0.5f;

        [Tooltip("충격파·균열 밝기 (재질 색에 곱함)")]
        [SerializeField] private float groundIntensity = 3f;

        [Tooltip("펑 뒤 여운(초)")]
        [SerializeField, Min(0f)] private float afterSeconds = 0.8f;

        [Tooltip("캡슐 중심에서 바닥까지의 높이 (캡슐 원점이 몸 가운데라 -1이 발밑)")]
        [SerializeField] private float groundOffset = -1f;

        // Sprites/Default 재질의 색 이름 (달 밝기를 바꿀 때 씀).
        private static readonly int SpriteColorId = Shader.PropertyToID("_Color");

        // 연출 컷을 건 카메라. 연출이 중간에 사라져도 카메라·캡슐·조명이 연출 상태로 남지 않게 OnDestroy에서 되돌린다.
        private BattleCameraDirector cinematicCamera;

        // 밤으로 바꾸기 전의 조명·안개 값 (끝나면 이 값으로 되돌린다).
        private bool nightApplied;
        private SphericalHarmonicsL2 dayAmbientProbe;
        private Color dayFogColor;
        private float dayFogStart;
        private float dayFogEnd;
        private Color dayBackground;
        private Camera viewCamera;
        private readonly List<(Light Light, float Intensity, Color Color)> dayLights = new List<(Light, float, Color)>();

        private void OnDestroy()
        {
            RestoreDay();
            if (cinematicCamera != null)
            {
                cinematicCamera.EndCinematic();
            }

            if (CasterView != null)
            {
                CasterView.ClearScriptedPose();
            }
        }

        public override IEnumerator Play(
            Vector3 caster,
            IReadOnlyList<(BattleUnit Unit, Vector3 Position)> targets,
            Action<BattleUnit, float> onImpact,
            BattleCameraDirector camera)
        {
            if (targets.Count == 0 || CasterView == null)
            {
                Destroy(gameObject);
                yield break;
            }

            (BattleUnit target, Vector3 targetPosition) = targets[0];
            Vector3 origin = CasterView.AnchorPosition;
            Vector3 forward = Flat(targetPosition - origin);
            Quaternion facing = Quaternion.LookRotation(forward);
            moon.gameObject.SetActive(false);

            Vector3 flatTarget = new Vector3(targetPosition.x, origin.y, targetPosition.z);
            Vector3 apex = Vector3.Lerp(origin, flatTarget, apexForward) + Vector3.up * apexHeight;
            Vector3 landing = flatTarget - forward * landShort;

            CasterView.SetScriptedPose(origin, facing);
            CaptureDay(camera);
            yield return NightAndVanish(origin, forward, camera);

            Vector3 cameraPosition = BeginEnemyView(targetPosition, forward, apex, camera);
            ShowMoon(cameraPosition, apex);
            yield return Appear(apex, facing);
            yield return Dive(apex, landing, targetPosition, forward, facing, target, onImpact, camera);

            yield return new WaitForSeconds(afterSeconds);
            yield return Animate(dayFadeSeconds, t => SetNight(1f - t, 0f));
            RestoreDay();

            // 컷과 함께 딜러를 원래 자리로 (카메라가 바뀌는 순간이라 순간 이동이 보이지 않음).
            if (cinematicCamera != null)
            {
                cinematicCamera.EndCinematic();
                cinematicCamera = null;
            }

            CasterView.ClearScriptedPose();
            Destroy(gameObject);
        }

        // 1) 딜러를 옆에서 잡는 시점으로 컷하고 밤이 내리며 안개가 짙어진다. vanishAt초에 딜러 발밑에서 안개 뭉치가 퍼지고 딜러가 사라진다.
        // 사라짐 = 캡슐을 바닥 아래 멀리 옮겨 둔다 (안개 뭉치가 덮은 순간이라 갑자기 없어진 게 보이지 않음).
        private IEnumerator NightAndVanish(Vector3 origin, Vector3 forward, BattleCameraDirector camera)
        {
            Vector3 side = PickCameraSide(origin, forward, camera);
            if (camera != null)
            {
                var lookPoint = new GameObject("VanishLook").transform;
                lookPoint.SetParent(transform, false);
                lookPoint.position = origin + Vector3.up * 0.2f;
                camera.BeginCinematic(origin + side * vanishViewDistance + Vector3.up * vanishViewHeight, lookPoint, vanishFieldOfView);
                cinematicCamera = camera;
            }

            bool vanished = false;
            float duration = Mathf.Max(vanishShotSeconds, nightFadeSeconds);
            yield return Animate(duration, t =>
            {
                float elapsed = t * duration;
                float night = Mathf.Clamp01(elapsed / nightFadeSeconds);
                SetNight(night, night);

                if (!vanished && elapsed >= vanishAt)
                {
                    vanished = true;
                    SpawnPuff(origin + Vector3.up * groundOffset);
                    CasterView.SetScriptedPose(origin + Vector3.down * 50f, Quaternion.LookRotation(forward));
                }
            });
        }

        // 2) 적 시점으로 컷: 대상 뒤(딜러 반대쪽)·옆·낮은 곳에서 정점 쪽을 올려다본다. 반환값: 카메라 자리 (달을 놓을 때 씀).
        private Vector3 BeginEnemyView(Vector3 targetPosition, Vector3 forward, Vector3 apex, BattleCameraDirector camera)
        {
            Vector3 side = Vector3.Cross(Vector3.up, forward);
            Vector3 position = targetPosition + forward * enemyViewBack + side * enemyViewSide + Vector3.up * enemyViewHeight;
            if (camera != null)
            {
                var lookPoint = new GameObject("EnemyLook").transform;
                lookPoint.SetParent(transform, false);
                lookPoint.position = apex;
                camera.BeginCinematic(position, lookPoint, enemyViewFieldOfView);
                cinematicCamera = camera;
            }

            return position;
        }

        // 달: 카메라 → 정점 방향으로 정점보다 moonDistance만큼 먼 하늘에 띄우고 카메라를 바라보게 한다 (딜러가 달을 등짐).
        // 달 재질(Sprites/Default)은 안개를 받지 않아 짙은 안개 속에서도 선명하다. 달은 하늘에 그대로 있다.
        private void ShowMoon(Vector3 cameraPosition, Vector3 apex)
        {
            Vector3 direction = (apex - cameraPosition).normalized;
            Vector3 position = cameraPosition + direction * moonDistance;
            moon.transform.position = position;
            moon.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            moon.transform.localScale = Vector3.one * moonSize;
            moon.gameObject.SetActive(true);

            var block = new MaterialPropertyBlock();
            Color color = moon.sharedMaterial != null && moon.sharedMaterial.HasProperty(SpriteColorId) ? moon.sharedMaterial.GetColor(SpriteColorId) : Color.white;
            block.SetColor(SpriteColorId, color * moonIntensity);
            moon.SetPropertyBlock(block);
        }

        // 딜러가 정점(달 앞)에 안개 뭉치와 함께 나타나 잠깐 떠 있다. 그동안 안개를 원래만큼 걷어 내 내려오는 모습이 보이게 한다.
        private IEnumerator Appear(Vector3 apex, Quaternion facing)
        {
            SpawnPuff(apex);
            if (trail != null)
            {
                trail.Play();
            }

            yield return Animate(hangSeconds, t =>
            {
                Vector3 position = apex + Vector3.up * 0.25f * Mathf.Sin(t * Mathf.PI * 0.5f);
                CasterView.SetScriptedPose(position, facing);
                FollowTrail(position);
                SetNight(1f, 1f - t);
            });
        }

        // 3) 내려찍기: 정점 → 착지점을 t²(가속)로 내리꽂힌다. 진행도가 topCutAt에 닿으면(내려찍기 직전) 위 시점으로 컷하고,
        //    착지 순간 펑: 폭발 + 섬광 + 충격파 고리 두 겹 + 균열 + 대상 피해(비율 1 = 전부) + 강한 흔들림.
        private IEnumerator Dive(
            Vector3 apex,
            Vector3 landing,
            Vector3 targetPosition,
            Vector3 forward,
            Quaternion facing,
            BattleUnit target,
            Action<BattleUnit, float> onImpact,
            BattleCameraDirector camera)
        {
            bool cut = false;
            Vector3 top = apex + Vector3.up * 0.25f;
            yield return Animate(diveSeconds, t =>
            {
                float progress = t * t;
                Vector3 position = Vector3.Lerp(top, landing, progress);
                CasterView.SetScriptedPose(position, facing);
                FollowTrail(position);

                if (!cut && progress >= topCutAt)
                {
                    cut = true;
                    BeginTopView(landing, forward, camera);
                }
            });

            if (!cut)
            {
                BeginTopView(landing, forward, camera);
            }

            if (trail != null)
            {
                trail.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }

            Vector3 ground = targetPosition + Vector3.up * (groundOffset + 0.03f);
            if (impactPrefab != null)
            {
                GameObject impact = Instantiate(impactPrefab, ground, Quaternion.identity);
                impact.transform.localScale = Vector3.one * impactScale;
            }

            if (flashPrefab != null)
            {
                GameObject flash = Instantiate(flashPrefab, targetPosition, Quaternion.identity);
                flash.transform.localScale = Vector3.one * flashScale;
            }

            if (groundLine != null)
            {
                StartCoroutine(Shockwave(ground, 0f));
                StartCoroutine(Shockwave(ground, secondRingDelay));
                StartCoroutine(Cracks(ground));
            }

            onImpact(target, 1f);
            if (camera != null)
            {
                camera.Shake(impactShake, impactShakeSeconds);
            }
        }

        // 위 시점으로 컷: 착지점 위 높은 곳(딜러 쪽으로 조금 물러남)에서 착지점 바닥을 내려다본다. 달은 이 시점에선 보이지 않으므로 끈다.
        private void BeginTopView(Vector3 landing, Vector3 forward, BattleCameraDirector camera)
        {
            moon.gameObject.SetActive(false);
            if (camera == null)
            {
                return;
            }

            Vector3 ground = landing + Vector3.up * groundOffset;
            var lookPoint = new GameObject("TopLook").transform;
            lookPoint.SetParent(transform, false);
            lookPoint.position = ground;
            camera.BeginCinematic(ground + Vector3.up * topViewHeight - forward * topViewBack, lookPoint, topViewFieldOfView);
            cinematicCamera = camera;
        }

        private void SpawnPuff(Vector3 position)
        {
            if (fogPuffPrefab != null)
            {
                GameObject puff = Instantiate(fogPuffPrefab, position, Quaternion.identity);
                puff.transform.localScale = Vector3.one * puffScale;
            }
        }

        // ---------- 밤·안개 ----------

        // 지금 조명·안개 값을 기억한다: 주변광(RenderSettings.ambientProbe = 장면 전체에 깔리는 빛), 안개 색·거리, 카메라 배경색, 방향광들의 밝기·색.
        private void CaptureDay(BattleCameraDirector camera)
        {
            dayAmbientProbe = RenderSettings.ambientProbe;
            dayFogColor = RenderSettings.fogColor;
            dayFogStart = RenderSettings.fogStartDistance;
            dayFogEnd = RenderSettings.fogEndDistance;
            viewCamera = camera != null && camera.Camera != null ? camera.Camera : Camera.main;
            dayBackground = viewCamera != null ? viewCamera.backgroundColor : Color.black;
            dayLights.Clear();

            // FindObjectsByType: 씬의 모든 조명. 해 역할인 방향광(Directional)만 밤빛으로 바꾼다.
            foreach (Light light in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type == LightType.Directional)
                {
                    dayLights.Add((light, light.intensity, light.color));
                }
            }

            nightApplied = true;
        }

        // night: 낮(0) ~ 밤(1) 조명 비율, fog: 원래 안개(0) ~ 짙은 안개(1) 비율.
        // 주변광은 SphericalHarmonicsL2(방향별 빛을 담는 값)로 다룬다: 밤 쪽은 AddAmbientLight로 만든 고른 남색 빛, 둘을 비율로 섞는다.
        // 안개가 짙어진다 = 안개 시작·끝 거리를 가깝게 당긴다 (선형 안개: 시작 거리부터 끝 거리까지 점점 가려짐).
        private void SetNight(float night, float fog)
        {
            if (!nightApplied)
            {
                return;
            }

            var nightProbe = new SphericalHarmonicsL2();
            nightProbe.AddAmbientLight(nightAmbient);
            RenderSettings.ambientProbe = dayAmbientProbe * (1f - night) + nightProbe * night;
            RenderSettings.fogColor = Color.Lerp(dayFogColor, nightSky, night);
            RenderSettings.fogStartDistance = Mathf.Lerp(dayFogStart, thickFogStart, fog);
            RenderSettings.fogEndDistance = Mathf.Lerp(dayFogEnd, thickFogEnd, fog);
            if (viewCamera != null)
            {
                viewCamera.backgroundColor = Color.Lerp(dayBackground, nightSky, night);
            }

            foreach ((Light light, float intensity, Color color) in dayLights)
            {
                if (light != null)
                {
                    light.intensity = Mathf.Lerp(intensity, intensity * nightLightScale, night);
                    light.color = Color.Lerp(color, nightLightColor, night);
                }
            }
        }

        // 기억해 둔 낮 조명·안개로 정확히 되돌린다 (연출이 중간에 사라져도 OnDestroy에서 부름).
        private void RestoreDay()
        {
            if (!nightApplied)
            {
                return;
            }

            SetNight(0f, 0f);
            nightApplied = false;
        }

        // ---------- 충격파·균열 ----------

        // 충격파 고리: 바닥에 눕힌 원이 delay초 뒤 퍼져 나가며 가늘어지고 사라진다.
        // 반지름은 1 - (1 - t)³로 처음에 확 퍼지고 점점 느려진다. 원 위의 점 = 중심 + (cos, 0, sin) × 반지름.
        private IEnumerator Shockwave(Vector3 center, float delay)
        {
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }

            var lines = new GlowLines(groundLine, transform);
            lines.SetIntensity(groundIntensity);
            const int points = 64;
            LineRenderer ring = lines.Create(points, loop: true);
            yield return Animate(ringSeconds, t =>
            {
                float radius = ringRadius * (1f - (1f - t) * (1f - t) * (1f - t));
                for (int i = 0; i < points; i++)
                {
                    float a = i / (float)points * Mathf.PI * 2f;
                    ring.SetPosition(i, center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius);
                }

                lines.SetWidthScale(Mathf.Lerp(1.6f, 0.2f, t));
                lines.SetAlpha(1f - t * t);
            });

            ring.enabled = false;
        }

        // 균열: 착지점에서 사방(고르게 + 무작위 비틀림)으로 지그재그 균열이 뻗고, 잠시 빛나다 사라진다.
        private IEnumerator Cracks(Vector3 center)
        {
            var lines = new GlowLines(groundLine, transform);
            lines.SetIntensity(groundIntensity);
            var cracks = new List<(LineRenderer Line, Vector3[] Points)>();
            float start = UnityEngine.Random.Range(0f, 360f);
            for (int i = 0; i < crackCount; i++)
            {
                // Quaternion.Euler(0, 각도, 0) * 앞: 앞 방향을 y축으로 그 각도만큼 돌린 수평 방향.
                Vector3 direction = Quaternion.Euler(0f, start + 360f / crackCount * i + UnityEngine.Random.Range(-15f, 15f), 0f) * Vector3.forward;
                Vector3 end = center + direction * crackLength * UnityEngine.Random.Range(0.6f, 1.15f);
                LineRenderer line = lines.Create(2);
                GlowLines.SetZigzag(line, center, end, 7, 0.3f);

                // 지그재그로 흔든 점들의 높이를 바닥에 맞춘다 (균열이 공중에 뜨지 않게).
                var points = new Vector3[line.positionCount];
                for (int p = 0; p < points.Length; p++)
                {
                    points[p] = line.GetPosition(p);
                    points[p].y = center.y;
                }

                cracks.Add((line, points));
            }

            lines.SetWidthScale(0.6f);
            yield return Animate(crackSeconds, t =>
            {
                foreach ((LineRenderer line, Vector3[] points) in cracks)
                {
                    GlowLines.DrawPartial(line, points, 1f - (1f - t) * (1f - t));
                }
            });

            yield return new WaitForSeconds(crackLingerSeconds);
            yield return Animate(0.4f, t =>
            {
                lines.SetAlpha(1f - t);
                lines.SetWidthScale(0.6f * (1f - t));
            });
        }

        // ---------- 공통 ----------

        // 카메라를 둘 옆 방향: 앞에 수직인 수평 방향 두 개 중 지금 카메라가 있는 쪽 (Dot < 0이면 반대쪽).
        private static Vector3 PickCameraSide(Vector3 origin, Vector3 forward, BattleCameraDirector camera)
        {
            Vector3 side = Vector3.Cross(Vector3.up, forward);
            Camera view = camera != null && camera.Camera != null ? camera.Camera : Camera.main;
            if (view != null && Vector3.Dot(side, view.transform.position - origin) < 0f)
            {
                side = -side;
            }

            return side;
        }

        // 빛 꼬리 파티클을 딜러 몸에 붙여 따라가게 한다.
        private void FollowTrail(Vector3 position)
        {
            if (trail != null)
            {
                trail.transform.position = position;
            }
        }

        // seconds 동안 0 → 1 진행도를 apply(람다)에 넘긴다 (전투 시간 기준이라 히트스톱이면 함께 멈춘다).
        private static IEnumerator Animate(float seconds, Action<float> apply)
        {
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.deltaTime;
                apply(Mathf.Clamp01(elapsed / seconds));
                yield return null;
            }

            apply(1f);
        }

        // 수평 방향만 남긴 단위 벡터 (0이면 앞).
        private static Vector3 Flat(Vector3 vector)
        {
            vector.y = 0f;
            return vector.sqrMagnitude > 0.0001f ? vector.normalized : Vector3.forward;
        }
    }
}

﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine.AddressableAssets;
using UnityEngine.Analytics;
using UnityEngine.ResourceManagement;
using UnityEngine.ResourceManagement.AsyncOperations;
using GameObject = UnityEngine.GameObject;
using System.Linq;
using System.Security.Cryptography;

#if UNITY_ANALYTICS
using UnityEngine.Analytics;
#endif

public class TrackManager : MonoBehaviour
{
	static public TrackManager instance { get { return s_Instance; } }
	static protected TrackManager s_Instance;

	static int s_StartHash = Animator.StringToHash("Start");

	public delegate int MultiplierModifier(int current);
	public MultiplierModifier modifyMultiply;

	[Header("Character & Movements")]
	public CharacterInputController characterController;
	public float minSpeed = 5.0f;
	public float maxSpeed = 10.0f;
	public int speedStep = 4;
	public float laneOffset = 1.0f;

	public bool invincible = false;

	[Header("Objects")]
	public ConsumableDatabase consumableDatabase;
	public MeshFilter skyMeshFilter;

	[Header("Parallax")]
	public Transform parallaxRoot;
	public float parallaxRatio = 0.5f;

	public System.Action<TrackSegment> newSegmentCreated;
	public System.Action<TrackSegment> currentSegementChanged;

	public int trackSeed { get { return m_TrackSeed; } set { m_TrackSeed = value; } }

	public float timeToStart { get { return m_TimeToStart; } }  // 이미 시작된 경우 -1을 반환(UI 업데이트 허용)

	public int score { get { return m_Score; } }
	public string currentTest { get { return m_CurrentTest; } }
	public int multiplier { get { return m_Multiplier; } }
	public float currentSegmentDistance { get { return m_CurrentSegmentDistance; } }
	public float worldDistance { get { return m_TotalWorldDistance; } }
	public float speed { get { return m_Speed; } }
	public float speedRatio { get { return (m_Speed - minSpeed) / (maxSpeed - minSpeed); } }
	public int currentZone { get { return m_CurrentZone; } }

	public TrackSegment currentSegment { get { return m_Segments[0]; } }
	public List<TrackSegment> segments { get { return m_Segments; } }
	public ThemeData currentTheme { get { return m_CurrentThemeData; } }

	public bool isMoving { get { return m_IsMoving; } }

	public bool isLoaded { get; set; }
	//중간 차선에 첫 번째 장애물을 생성해야 하기 때문에 튜토리얼의 장애물 생성 코드에서 사용됨.
	public bool firstObstacle { get; set; }

	protected float m_TimeToStart = -1.0f;
	// 이 값이 -1로 설정되면 무작위 시드가 시스템 시계로 초기화되고, 그렇지 않으면 해당 값으로 초기화됨.
	// 동일한 게임을 여러 번 플레이할 수 있도록 허용(플레이어 간 특정 경쟁/도전 공정성을 만드는 데 유용함)
	protected int m_TrackSeed = -1;

	protected float m_CurrentSegmentDistance;
	protected float m_TotalWorldDistance;
	protected bool m_IsMoving;
	protected float m_Speed;

	protected float m_TimeSincePowerup;     // 높을수록 스폰될 확률이 높아짐.
	protected float m_TimeSinceLastPremium;

	protected int m_Multiplier;
	protected int m_scoreCombo;

	protected List<TrackSegment> m_Segments = new List<TrackSegment>();
	protected List<TrackSegment> m_PastSegments = new List<TrackSegment>();
	protected int m_SafeSegementLeft;

	protected ThemeData m_CurrentThemeData;
	protected int m_CurrentZone;
	protected float m_CurrentZoneDistance;
	protected int m_PreviousSegment = -1;

	protected int m_Score;
	protected string m_CurrentTest;
	protected float m_ScoreAccum;

	protected Queue<string> m_TestQueue = new Queue<string>();

	Vector3 m_CameraOriginalPos = Vector3.zero;

	const float k_FloatingOriginThreshold = 10000f;

	protected const float k_CountdownToStartLength = 5f;
	protected const float k_CountdownSpeed = 1.5f;
	protected const float k_StartingSegmentDistance = 2f;
	protected const int k_StartingSafeSegments = 2;
	protected const int k_StartingCoinPoolSize = 256;
	protected const int k_DesiredSegmentCount = 10;
	protected const float k_SegmentRemovalDistance = -30f;
	protected const float k_Acceleration = 0.2f;

	protected void Awake()
	{
		m_ScoreAccum = 0.0f;
		s_Instance = this;
	}


	public void StartMove(bool isRestart = true)
	{
		characterController.StartMoving();
		m_IsMoving = true;
		if (isRestart)
			m_Speed = minSpeed;
	}

	public void StopMove()
	{
		m_IsMoving = false;
	}

	IEnumerator WaitToStart()
	{
		characterController.character.animator.Play(s_StartHash);
		float length = k_CountdownToStartLength;
		m_TimeToStart = length;

		while (m_TimeToStart >= 0)
		{
			yield return null;
			m_TimeToStart -= Time.deltaTime * k_CountdownSpeed;
		}

		m_TimeToStart = -1;

		characterController.StartRunning();
		StartMove();
	}

	public IEnumerator Begin()
	{
		firstObstacle = true;
		m_CameraOriginalPos = Camera.main.transform.position;

		if (m_TrackSeed != -1)
			Random.InitState(m_TrackSeed);
		else
			Random.InitState((int)System.DateTime.Now.Ticks);

		// 재실행이 아니기 때문에 전체 시스템을 초기화. (재실행 시 사망 시의 상태를 유지하고 싶을때)
		m_CurrentSegmentDistance = k_StartingSegmentDistance;
		m_TotalWorldDistance = 0.0f;

		characterController.gameObject.SetActive(true);

		//Addressables 1.0.1-preview
		var op = Addressables.InstantiateAsync(PlayerData.instance.character,
			Vector3.zero,
			Quaternion.identity);
		yield return op;
		if (op.Result == null || !(op.Result is GameObject))
		{
			Debug.LogWarning(string.Format("Unable to load character {0}.", PlayerData.instance.character));
			yield break;
		}
		Character player = op.Result.GetComponent<Character>();

		characterController.character = player;
		characterController.trackManager = this;

		characterController.Init();
		characterController.CheatInvincible(invincible);

		player.transform.SetParent(characterController.characterCollider.transform, false);
		Camera.main.transform.SetParent(characterController.transform, true);

		m_CurrentThemeData = ThemeDatabase.GetThemeData(PlayerData.instance.theme);

		m_CurrentZone = 0;
		m_CurrentZoneDistance = 0;

		skyMeshFilter.sharedMesh = m_CurrentThemeData.skyMesh;
		RenderSettings.fogColor = m_CurrentThemeData.fogColor;
		RenderSettings.fog = true;

		gameObject.SetActive(true);
		characterController.gameObject.SetActive(true);

		m_Score = 0;
		m_ScoreAccum = 0;
		m_CurrentTest = string.Empty;

		m_SafeSegementLeft = k_StartingSafeSegments;

		Coin.coinPool = new Pooler(currentTheme.collectiblePrefab, k_StartingCoinPoolSize);

		characterController.Begin();
		StartCoroutine(WaitToStart());
		isLoaded = true;
	}

	public void End()
	{
		foreach (TrackSegment seg in m_Segments)
		{
			Addressables.ReleaseInstance(seg.gameObject);
			_spawnedSegments--;
		}

		for (int i = 0; i < m_PastSegments.Count; ++i)
		{
			Addressables.ReleaseInstance(m_PastSegments[i].gameObject);
		}

		m_Segments.Clear();
		m_PastSegments.Clear();

		characterController.End();

		gameObject.SetActive(false);
		Addressables.ReleaseInstance(characterController.character.gameObject);
		characterController.character = null;

		Camera.main.transform.SetParent(null);
		Camera.main.transform.position = m_CameraOriginalPos;

		characterController.gameObject.SetActive(false);

		for (int i = 0; i < parallaxRoot.childCount; ++i)
		{
			_parallaxRootChildren--;
			Destroy(parallaxRoot.GetChild(i).gameObject);
		}
	}


	private int _parallaxRootChildren = 0;
	private int _spawnedSegments = 0;
	void Update()
	{
		while (_spawnedSegments < k_DesiredSegmentCount)
		{
			StartCoroutine(SpawnNewSegment());
			_spawnedSegments++;
		}

		if (parallaxRoot != null && currentTheme.cloudPrefabs.Length > 0)
		{
			while (_parallaxRootChildren < currentTheme.cloudNumber)
			{
				float lastZ = parallaxRoot.childCount == 0 ? 0 : parallaxRoot.GetChild(parallaxRoot.childCount - 1).position.z + currentTheme.cloudMinimumDistance.z;

				GameObject cloud = currentTheme.cloudPrefabs[Random.Range(0, currentTheme.cloudPrefabs.Length)];
				if (cloud != null)
				{
					GameObject obj = Instantiate(cloud);
					obj.transform.SetParent(parallaxRoot, false);

					obj.transform.localPosition =
						Vector3.up * (currentTheme.cloudMinimumDistance.y +
									  (Random.value - 0.5f) * currentTheme.cloudSpread.y)
						+ Vector3.forward * (lastZ + (Random.value - 0.5f) * currentTheme.cloudSpread.z)
						+ Vector3.right * (currentTheme.cloudMinimumDistance.x +
										   (Random.value - 0.5f) * currentTheme.cloudSpread.x);

					obj.transform.localScale = obj.transform.localScale * (1.0f + (Random.value - 0.5f) * 0.5f);
					obj.transform.localRotation = Quaternion.AngleAxis(Random.value * 360.0f, Vector3.up);
					_parallaxRootChildren++;
				}
			}
		}

		if (!m_IsMoving)
			return;

		float scaledSpeed = m_Speed * Time.deltaTime;
		m_CurrentZoneDistance += scaledSpeed;

		//m_ScoreAccum += scaledSpeed;
		//int intScore = Mathf.FloorToInt(m_ScoreAccum);
		//if (intScore != 0) AddScore(intScore);
		//m_ScoreAccum -= intScore;

		m_TotalWorldDistance += scaledSpeed;
		m_CurrentSegmentDistance += scaledSpeed;

		if (m_CurrentSegmentDistance > m_Segments[0].worldLength)
		{
			m_CurrentSegmentDistance -= m_Segments[0].worldLength;

			// m_PastSegments are segment we already passed, we keep them to move them and destroy them later 
			// but they aren't part of the game anymore 
			m_PastSegments.Add(m_Segments[0]);
			m_Segments.RemoveAt(0);
			_spawnedSegments--;

			if (currentSegementChanged != null) currentSegementChanged.Invoke(m_Segments[0]);
		}

		Vector3 currentPos;
		Quaternion currentRot;
		Transform characterTransform = characterController.transform;

		m_Segments[0].GetPointAtInWorldUnit(m_CurrentSegmentDistance, out currentPos, out currentRot);


		// Floating origin implementation
		bool needRecenter = currentPos.sqrMagnitude > k_FloatingOriginThreshold;

		// Parallax Handling
		if (parallaxRoot != null)
		{
			Vector3 difference = (currentPos - characterTransform.position) * parallaxRatio; ;
			int count = parallaxRoot.childCount;
			for (int i = 0; i < count; i++)
			{
				Transform cloud = parallaxRoot.GetChild(i);
				cloud.position += difference - (needRecenter ? currentPos : Vector3.zero);
			}
		}

		if (needRecenter)
		{
			int count = m_Segments.Count;
			for (int i = 0; i < count; i++)
			{
				m_Segments[i].transform.position -= currentPos;
			}

			count = m_PastSegments.Count;
			for (int i = 0; i < count; i++)
			{
				m_PastSegments[i].transform.position -= currentPos;
			}

			m_Segments[0].GetPointAtInWorldUnit(m_CurrentSegmentDistance, out currentPos, out currentRot);
		}

		characterTransform.rotation = currentRot;
		characterTransform.position = currentPos;

		if (parallaxRoot != null && currentTheme.cloudPrefabs.Length > 0)
		{
			for (int i = 0; i < parallaxRoot.childCount; ++i)
			{
				Transform child = parallaxRoot.GetChild(i);

				// 불필요한 클라우드 제거
				if ((child.localPosition - currentPos).z < -50)
				{
					_parallaxRootChildren--;
					Destroy(child.gameObject);
				}
			}
		}

		for (int i = 0; i < m_PastSegments.Count; ++i)
		{
			if ((m_PastSegments[i].transform.position - currentPos).z < k_SegmentRemovalDistance)
			{
				m_PastSegments[i].Cleanup();
				m_PastSegments.RemoveAt(i);
				i--;
			}
		}

		PowerupSpawnUpdate();

		if (m_Speed < maxSpeed)
			m_Speed += k_Acceleration * Time.deltaTime;
		else
			m_Speed = maxSpeed;

		m_Multiplier = 1 + Mathf.FloorToInt((m_Speed - minSpeed) / (maxSpeed - minSpeed) * speedStep);

		if (modifyMultiply != null)
		{
			foreach (MultiplierModifier part in modifyMultiply.GetInvocationList())
			{
				m_Multiplier = part(m_Multiplier);
			}
		}

		MusicPlayer.instance.UpdateVolumes(speedRatio);
	}

	public void PowerupSpawnUpdate()
	{
		m_TimeSincePowerup += Time.deltaTime;
		m_TimeSinceLastPremium += Time.deltaTime;
	}

	public void ChangeZone()
	{
		m_CurrentZone += 1;
		if (m_CurrentZone >= m_CurrentThemeData.zones.Length)
			m_CurrentZone = 0;

		m_CurrentZoneDistance = 0;
	}

	private readonly Vector3 _offScreenSpawnPos = new Vector3(-100f, -100f, -100f);
	public IEnumerator SpawnNewSegment()
	{
		if (m_CurrentThemeData.zones[m_CurrentZone].length < m_CurrentZoneDistance)
			ChangeZone();

		int segmentUse = Random.Range(0, m_CurrentThemeData.zones[m_CurrentZone].prefabList.Length);
		if (segmentUse == m_PreviousSegment) segmentUse = (segmentUse + 1) % m_CurrentThemeData.zones[m_CurrentZone].prefabList.Length;

		AsyncOperationHandle segmentToUseOp = m_CurrentThemeData.zones[m_CurrentZone].prefabList[segmentUse].InstantiateAsync(_offScreenSpawnPos, Quaternion.identity);
		yield return segmentToUseOp;
		if (segmentToUseOp.Result == null || !(segmentToUseOp.Result is GameObject))
		{
			Debug.LogWarning(string.Format("Unable to load segment {0}.", m_CurrentThemeData.zones[m_CurrentZone].prefabList[segmentUse].Asset.name));
			yield break;
		}
		TrackSegment newSegment = (segmentToUseOp.Result as GameObject).GetComponent<TrackSegment>();

		Vector3 currentExitPoint;
		Quaternion currentExitRotation;
		if (m_Segments.Count > 0)
		{
			m_Segments[m_Segments.Count - 1].GetPointAt(1.0f, out currentExitPoint, out currentExitRotation);
		}
		else
		{
			currentExitPoint = transform.position;
			currentExitRotation = transform.rotation;
		}

		newSegment.transform.rotation = currentExitRotation;

		Vector3 entryPoint;
		Quaternion entryRotation;
		newSegment.GetPointAt(0.0f, out entryPoint, out entryRotation);


		Vector3 pos = currentExitPoint + (newSegment.transform.position - entryPoint);
		newSegment.transform.position = pos;
		newSegment.manager = this;

		newSegment.transform.localScale = new Vector3((Random.value > 0.5f ? -1 : 1), 1, 1);
		newSegment.objectRoot.localScale = new Vector3(1.0f / newSegment.transform.localScale.x, 1, 1);

		if (m_SafeSegementLeft <= 0)
		{
			SpawnObstacle(newSegment);
		}
		else
			m_SafeSegementLeft -= 1;

		m_Segments.Add(newSegment);

		if (newSegmentCreated != null) newSegmentCreated.Invoke(newSegment);
	}


	public void SpawnObstacle(TrackSegment segment)
	{
		if (segment.possibleObstacles.Length != 0)
		{
			for (int i = 0; i < segment.obstaclePositions.Length; ++i)
			{
				AssetReference assetRef = segment.possibleObstacles[Random.Range(0, segment.possibleObstacles.Length)];
				StartCoroutine(SpawnFromAssetReference(assetRef, segment, i));
			}
		}

		m_SafeSegementLeft = k_StartingSafeSegments;

		StartCoroutine(SpawnCoinAndPowerup(segment));
	}

	private IEnumerator SpawnFromAssetReference(AssetReference reference, TrackSegment segment, int posIndex)
	{
		AsyncOperationHandle op = Addressables.LoadAssetAsync<GameObject>(reference);
		yield return op;
		GameObject obj = op.Result as GameObject;
		if (obj != null)
		{
			Obstacle obstacle = obj.GetComponent<Obstacle>();
			if (obstacle != null)
			{
				if (obstacle is TestObstacle)
				{
					// TODO : 엑셀 페이지를 구분하는 기능을 만들어야 함.
					Dictionary<string, string> word = WordManager.Instance.GetRandomWordSet("900");

					if (m_CurrentTest.Equals(string.Empty))
						m_CurrentTest = word["Test"];
					else
						m_TestQueue.Enqueue(word["Test"]);

					(obstacle as TestObstacle).ApplyTest(word["Answer"], word["Wrong1"], word["Wrong2"]);
				}

				yield return obstacle.Spawn(segment, segment.obstaclePositions[posIndex]);
			}
		}
	}

	public IEnumerator SpawnCoinAndPowerup(TrackSegment segment)
	{
		const float increment = 1.5f;
		float currentWorldPos = 0.0f;
		int currentLane = Random.Range(0, 3);

		float powerupChance = Mathf.Clamp01(Mathf.Floor(m_TimeSincePowerup) * 0.5f * 0.001f);

		while (currentWorldPos < segment.worldLength)
		{
			Vector3 pos;
			Quaternion rot;
			segment.GetPointAtInWorldUnit(currentWorldPos, out pos, out rot);


			bool laneValid = true;
			int testedLane = currentLane;
			while (Physics.CheckSphere(pos + ((testedLane - 1) * laneOffset * (rot * Vector3.right)), 0.4f, 1 << 9))
			{
				testedLane = (testedLane + 1) % 3;
				if (currentLane == testedLane)
				{
					laneValid = false;
					break;
				}
			}

			currentLane = testedLane;

			if (laneValid)
			{
				pos = pos + ((currentLane - 1) * laneOffset * (rot * Vector3.right));

				GameObject toUse = null;
				if (Random.value < powerupChance)
				{
					int picked = Random.Range(0, consumableDatabase.consumbales.Length);

					if (consumableDatabase.consumbales[picked].canBeSpawned)
					{
						m_TimeSincePowerup = 0.0f;
						powerupChance = 0.0f;

						AsyncOperationHandle op = Addressables.InstantiateAsync(consumableDatabase.consumbales[picked].gameObject.name, pos, rot);
						yield return op;
						if (op.Result == null || !(op.Result is GameObject))
						{
							Debug.LogWarning(string.Format("Unable to load consumable {0}.", consumableDatabase.consumbales[picked].gameObject.name));
							yield break;
						}
						toUse = op.Result as GameObject;
						toUse.transform.SetParent(segment.transform, true);
					}
				}
				else
				{
					toUse = Coin.coinPool.Get(pos + 5 * Vector3.back, rot);
					toUse.transform.SetParent(segment.collectibleTransform, true);
				}

				if (toUse != null)
				{
					//TODO : remove that hack related to #issue7
					Vector3 oldPos = toUse.transform.position;
					toUse.transform.position += Vector3.back;
					toUse.transform.position = oldPos;
				}
			}

			currentWorldPos += increment;
		}
	}

	public void AddScore(int amount, int combo = 0)
	{
		int finalAmount = amount;
		m_Score += finalAmount * m_Multiplier;
	}
	public void SetToNextTest()
	{
		m_CurrentTest = m_TestQueue.TryDequeue(out string test) ? test : string.Empty;
	}
}
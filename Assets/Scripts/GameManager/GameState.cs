using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

#if UNITY_ADS
using UnityEngine.Advertisements;
#endif
#if UNITY_ANALYTICS
using UnityEngine.Analytics;
#endif

public class GameState : AState
{
	static int s_DeadHash = Animator.StringToHash("Dead");

	public Canvas canvas;
	public TrackManager trackManager;

	public AudioClip gameTheme;

	[Header("UI")]
	public Text scoreText;
	public Text multiplierText;
	public Text countdownText;
	public Text toeicTestText;
	public RectTransform powerupZone;
	public RectTransform lifeRectTransform;

	public RectTransform pauseMenu;
	public RectTransform wholeUI;
	public Button pauseButton;

	public GameObject gameOverPopup;

	[Header("Prefabs")]
	public GameObject PowerupIconPrefab;

	public Modifier currentModifier = new Modifier();

	protected bool m_Finished;
	protected float m_TimeSinceStart;
	protected List<PowerupIcon> m_PowerupIcons = new List<PowerupIcon>();
	protected Image[] m_LifeHearts;

	protected RectTransform m_CountdownRectTransform;
	protected bool m_WasMoving;

	protected bool m_AdsInitialised = false;
	protected bool m_GameoverSelectionDone = false;

	protected int k_MaxLives = 3;

	protected bool m_CountObstacles = true;
	protected bool m_DisplayTutorial;
	protected int m_CurrentSegmentObstacleIndex = 0;
	protected TrackSegment m_NextValidSegment = null;
	protected int k_ObstacleToClear = 3;

	public override void Enter(AState from)
	{
		m_CountdownRectTransform = countdownText.GetComponent<RectTransform>();

		m_LifeHearts = new Image[k_MaxLives];
		for (int i = 0; i < k_MaxLives; ++i)
		{
			m_LifeHearts[i] = lifeRectTransform.GetChild(i).GetComponent<Image>();
		}

		if (MusicPlayer.instance.GetStem(0) != gameTheme)
		{
			MusicPlayer.instance.SetStem(0, gameTheme);
			CoroutineHandler.StartStaticCoroutine(MusicPlayer.instance.RestartAllStems());
		}

		m_AdsInitialised = false;
		m_GameoverSelectionDone = false;

		StartGame();
	}

	public override void Exit(AState to)
	{
		canvas.gameObject.SetActive(false);

		PlayerData.instance.Save();

		trackManager.End();
	}

	public void StartGame()
	{
		canvas.gameObject.SetActive(true);
		pauseMenu.gameObject.SetActive(false);
		wholeUI.gameObject.SetActive(true);
		pauseButton.gameObject.SetActive(true);
		gameOverPopup.SetActive(false);

		m_TimeSinceStart = 0;
		trackManager.characterController.currentLife = trackManager.characterController.maxLife;
		currentModifier.OnRunStart(this);


		m_Finished = false;

		StartCoroutine(trackManager.Begin());
	}

	public override string GetName()
	{
		return "Game";
	}

	public override void Tick()
	{
		if (m_Finished)
		{

			return;
		}

		if (trackManager.isLoaded)
		{
			CharacterInputController chrCtrl = trackManager.characterController;

			m_TimeSinceStart += Time.deltaTime;

			if (chrCtrl.currentLife <= 0)
			{
				pauseButton.gameObject.SetActive(false);
				chrCtrl.CleanConsumable();
				chrCtrl.character.animator.SetBool(s_DeadHash, true);
				chrCtrl.characterCollider.koParticle.gameObject.SetActive(true);
				StartCoroutine(WaitForGameOver());
			}

			List<Consumable> toRemove = new List<Consumable>();
			List<PowerupIcon> toRemoveIcon = new List<PowerupIcon>();

			for (int i = 0; i < chrCtrl.consumables.Count; ++i)
			{
				PowerupIcon icon = null;
				for (int j = 0; j < m_PowerupIcons.Count; ++j)
				{
					if (m_PowerupIcons[j].linkedConsumable == chrCtrl.consumables[i])
					{
						icon = m_PowerupIcons[j];
						break;
					}
				}

				chrCtrl.consumables[i].Tick(chrCtrl);
				if (!chrCtrl.consumables[i].active)
				{
					toRemove.Add(chrCtrl.consumables[i]);
					toRemoveIcon.Add(icon);
				}
				else if (icon == null)
				{
					GameObject o = Instantiate(PowerupIconPrefab);

					icon = o.GetComponent<PowerupIcon>();

					icon.linkedConsumable = chrCtrl.consumables[i];
					icon.transform.SetParent(powerupZone, false);

					m_PowerupIcons.Add(icon);
				}
			}

			for (int i = 0; i < toRemove.Count; ++i)
			{
				toRemove[i].Ended(trackManager.characterController);

				Addressables.ReleaseInstance(toRemove[i].gameObject);
				if (toRemoveIcon[i] != null)
					Destroy(toRemoveIcon[i].gameObject);

				chrCtrl.consumables.Remove(toRemove[i]);
				m_PowerupIcons.Remove(toRemoveIcon[i]);
			}

			UpdateUI();

			currentModifier.OnRunTick(this);
		}
	}

	void OnApplicationPause(bool pauseStatus)
	{
		if (pauseStatus) Pause();
	}

	void OnApplicationFocus(bool focusStatus)
	{
		if (!focusStatus) Pause();
	}

	public void Pause(bool displayMenu = true)
	{
		if (m_Finished || AudioListener.pause == true)
			return;

		AudioListener.pause = true;
		Time.timeScale = 0;

		pauseButton.gameObject.SetActive(false);
		pauseMenu.gameObject.SetActive(displayMenu);
		wholeUI.gameObject.SetActive(false);
		m_WasMoving = trackManager.isMoving;
		trackManager.StopMove();
	}

	public void Resume()
	{
		Time.timeScale = 1.0f;
		pauseButton.gameObject.SetActive(true);
		pauseMenu.gameObject.SetActive(false);
		wholeUI.gameObject.SetActive(true);
		if (m_WasMoving)
		{
			trackManager.StartMove(false);
		}

		AudioListener.pause = false;
	}

	public void QuitToLoadout()
	{
		Time.timeScale = 1.0f;
		AudioListener.pause = false;
		trackManager.End();
		PlayerData.instance.Save();
		manager.SwitchState("Loadout");
	}

	protected void UpdateUI()
	{
		for (int i = 0; i < 3; ++i)
		{

			if (trackManager.characterController.currentLife > i)
			{
				m_LifeHearts[i].color = Color.white;
			}
			else
			{
				m_LifeHearts[i].color = Color.black;
			}
		}

		toeicTestText.text = trackManager.currentTest.ToString();
		scoreText.text = trackManager.score.ToString();
		multiplierText.text = "x " + trackManager.multiplier;

		if (trackManager.timeToStart >= 0)
		{
			countdownText.gameObject.SetActive(true);
			countdownText.text = Mathf.Ceil(trackManager.timeToStart).ToString();
			m_CountdownRectTransform.localScale = Vector3.one * (1.0f - (trackManager.timeToStart - Mathf.Floor(trackManager.timeToStart)));
		}
		else
		{
			m_CountdownRectTransform.localScale = Vector3.zero;
		}
	}

	IEnumerator WaitForGameOver()
	{
		m_Finished = true;
		trackManager.StopMove();

		Shader.SetGlobalFloat("_BlinkingValue", 0.0f);

		yield return new WaitForSeconds(2.0f);
		if (currentModifier.OnRunEnd(this))
		{
			OpenGameOverPopup();
		}
	}

	public void OpenGameOverPopup()
	{
		gameOverPopup.SetActive(true);
	}

	public void GameOver()
	{
		manager.SwitchState("Loadout");
	}


	public void ShowRewardedAd()
	{
		if (m_GameoverSelectionDone)
			return;

		m_GameoverSelectionDone = true;

		GameOver();
	}

}

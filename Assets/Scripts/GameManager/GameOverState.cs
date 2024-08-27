using UnityEngine;
using UnityEngine.UI;
#if UNITY_ANALYTICS
using UnityEngine.Analytics;
#endif
using System.Collections.Generic;


public class GameOverState : AState
{
	public TrackManager trackManager;
	public Canvas canvas;

	public AudioClip gameOverTheme;

	public GameObject addButton;

	public override void Enter(AState from)
	{
		canvas.gameObject.SetActive(true);

		PlayerData.instance.Save();

		if (MusicPlayer.instance.GetStem(0) != gameOverTheme)
		{
			MusicPlayer.instance.SetStem(0, gameOverTheme);
			StartCoroutine(MusicPlayer.instance.RestartAllStems());
		}
	}

	public override void Exit(AState to)
	{
		canvas.gameObject.SetActive(false);
		FinishRun();
	}

	public override string GetName()
	{
		return "GameOver";
	}

	public override void Tick()
	{

	}
	public void GoToStore()
	{
		UnityEngine.SceneManagement.SceneManager.LoadScene("shop", UnityEngine.SceneManagement.LoadSceneMode.Additive);
	}


	public void GoToLoadout()
	{
		manager.SwitchState("Loadout");
	}

	public void RunAgain()
	{
		manager.SwitchState("Game");
	}


	protected void FinishRun()
	{
		CharacterCollider.DeathEvent de = trackManager.characterController.characterCollider.deathData;

		PlayerData.instance.Save();

		trackManager.End();
	}

}

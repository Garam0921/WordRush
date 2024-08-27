using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.AddressableAssets;

[RequireComponent(typeof(AudioSource))]
public class CharacterCollider : MonoBehaviour
{
	static int s_HitHash = Animator.StringToHash("Hit");
	static int s_BlinkingValueHash;

	public struct DeathEvent
	{
		public string character;
		public string obstacleType;
		public string themeUsed;
		public int score;
		public float worldDistance;
	}

	public CharacterInputController controller;

	public ParticleSystem koParticle;

	[Header("Sound")]
	public AudioClip coinSound;

	public DeathEvent deathData { get { return m_DeathData; } }
	public new BoxCollider collider { get { return m_Collider; } }

	public new AudioSource audio { get { return m_Audio; } }

	protected bool m_Invincible;
	protected DeathEvent m_DeathData;
	protected BoxCollider m_Collider;
	protected AudioSource m_Audio;

	protected float m_StartingColliderHeight;
	protected int m_Combo = 0;

	protected readonly Vector3 k_SlidingColliderScale = new Vector3(1.0f, 0.5f, 1.0f);
	protected readonly Vector3 k_NotSlidingColliderScale = new Vector3(1.0f, 2.0f, 1.0f);

	protected const int k_DefaultScore = 10;
	protected const int k_AnswerLayerIndex = 7;
	protected const int k_CoinsLayerIndex = 8;
	protected const int k_ObstacleLayerIndex = 9;
	protected const int k_PowerupLayerIndex = 10;
	protected const float k_DefaultInvinsibleTime = 2f;

	protected void Start()
	{
		m_Collider = GetComponent<BoxCollider>();
		m_Audio = GetComponent<AudioSource>();
		m_StartingColliderHeight = m_Collider.bounds.size.y;
	}

	public void Init()
	{
		koParticle.gameObject.SetActive(false);

		s_BlinkingValueHash = Shader.PropertyToID("_BlinkingValue");
		m_Invincible = false;
		m_Combo = 0;
	}

	public void Slide(bool sliding)
	{
		if (sliding)
		{
			m_Collider.size = Vector3.Scale(m_Collider.size, k_SlidingColliderScale);
			m_Collider.center = m_Collider.center - new Vector3(0.0f, m_Collider.size.y * 0.5f, 0.0f);
		}
		else
		{
			m_Collider.center = m_Collider.center + new Vector3(0.0f, m_Collider.size.y * 0.5f, 0.0f);
			m_Collider.size = Vector3.Scale(m_Collider.size, k_NotSlidingColliderScale);
		}
	}

	protected void OnTriggerEnter(Collider c)
	{

		if (c.gameObject.layer == k_CoinsLayerIndex)
		{
			Coin.coinPool.Free(c.gameObject);
			PlayerData.instance.coins += 1;
			controller.coins += 1;
			m_Audio.PlayOneShot(coinSound);

		}
		else if (c.gameObject.layer == k_ObstacleLayerIndex)
		{
			if (m_Invincible || controller.IsCheatInvincible())
				return;

			controller.StopMoving();

			c.enabled = false;

			Obstacle ob = c.gameObject.GetComponent<Obstacle>();
			string obstacleType = "Obstacle";

			if (ob != null)
			{
				ob.Impacted();
				obstacleType = ob.GetType().ToString();
			}
			else
			{
				Addressables.ReleaseInstance(c.gameObject);
			}

			m_Combo = 0;

			controller.currentLife -= 1;

			controller.character.animator.SetTrigger(s_HitHash);

			if (controller.currentLife > 0)
			{
				m_Audio.PlayOneShot(controller.character.hitSound);
				SetInvincible();
			}
			// The collision killed the player, record all data to analytics.
			else
			{
				m_Audio.PlayOneShot(controller.character.deathSound);

				m_DeathData.character = controller.character.characterName;
				m_DeathData.themeUsed = controller.trackManager.currentTheme.themeName;
				m_DeathData.obstacleType = obstacleType;
				m_DeathData.score = controller.trackManager.score;
				m_DeathData.worldDistance = controller.trackManager.worldDistance;

			}
		}
		else if (c.gameObject.layer == k_AnswerLayerIndex)
		{
			// TODO : 콤보 공식이 결정되면 적용해야 함.
			controller.trackManager.SetToNextTest();
			controller.trackManager.AddScore(10 + m_Combo);
			m_Combo = Mathf.Min(5, ++m_Combo);
		}
		else if (c.gameObject.layer == k_PowerupLayerIndex)
		{
			Consumable consumable = c.GetComponent<Consumable>();
			if (consumable != null)
			{
				controller.UseConsumable(consumable);
			}
		}
	}

	public void SetInvincibleExplicit(bool invincible)
	{
		m_Invincible = invincible;
	}

	public void SetInvincible(float timer = k_DefaultInvinsibleTime)
	{
		StartCoroutine(InvincibleTimer(timer));
	}

	protected IEnumerator InvincibleTimer(float timer)
	{
		m_Invincible = true;

		float time = 0;
		float currentBlink = 1.0f;
		float lastBlink = 0.0f;
		const float blinkPeriod = 0.1f;

		while (time < timer && m_Invincible)
		{
			Shader.SetGlobalFloat(s_BlinkingValueHash, currentBlink);

			yield return null;
			time += Time.deltaTime;
			lastBlink += Time.deltaTime;

			if (blinkPeriod < lastBlink)
			{
				lastBlink = 0;
				currentBlink = 1.0f - currentBlink;
			}
		}

		Shader.SetGlobalFloat(s_BlinkingValueHash, 0.0f);

		m_Invincible = false;
	}
}

using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

#if UNITY_ANALYTICS
using UnityEngine.Analytics;
#endif

public class LoadoutState : AState
{
    public Canvas inventoryCanvas;

    [Header("Char UI")]
	public Transform charPosition;

	[Header("Other Data")]
	public Button runButton;

	public MeshFilter skyMeshFilter;
    public MeshFilter UIGroundFilter;

	public AudioClip menuTheme;

    protected GameObject m_Character;
    protected bool m_IsLoadingCharacter;

	protected Modifier m_CurrentModifier = new Modifier();

    protected const float k_CharacterRotationSpeed = 45f;
    protected const float k_OwnedAccessoriesCharacterOffset = -0.1f;
    protected int k_UILayer;
    protected readonly Quaternion k_FlippedYAxisRotation = Quaternion.Euler (0f, 180f, 0f);

    public override void Enter(AState from)
    {
        inventoryCanvas.gameObject.SetActive(true);

        k_UILayer = LayerMask.NameToLayer("UI");

        skyMeshFilter.gameObject.SetActive(true);
        UIGroundFilter.gameObject.SetActive(true);

        Shader.SetGlobalFloat("_BlinkingValue", 0.0f);

        if (MusicPlayer.instance.GetStem(0) != menuTheme)
		{
            MusicPlayer.instance.SetStem(0, menuTheme);
            StartCoroutine(MusicPlayer.instance.RestartAllStems());
        }

        runButton.interactable = false;
        runButton.GetComponentInChildren<Text>().text = "Loading...";

        Refresh();
    }

    public override void Exit(AState to)
    {
        inventoryCanvas.gameObject.SetActive(false);

        if (m_Character != null) Addressables.ReleaseInstance(m_Character);

        GameState gs = to as GameState;

        skyMeshFilter.gameObject.SetActive(false);
        UIGroundFilter.gameObject.SetActive(false);

        if (gs != null)
        {
			gs.currentModifier = m_CurrentModifier;

            m_CurrentModifier = new Modifier();
        }
    }

    public void Refresh()
    {
        StartCoroutine(PopulateCharacters());
    }

    public override string GetName()
    {
        return "Loadout";
    }

    public override void Tick()
    {
        if (!runButton.interactable)
        {
            bool interactable = CharacterDatabase.loaded;
            if(interactable)
            {
                runButton.interactable = true;
                runButton.GetComponentInChildren<Text>().text = "START";
            }
        }

        if(m_Character != null)
        {
            m_Character.transform.Rotate(0, k_CharacterRotationSpeed * Time.deltaTime, 0, Space.Self);
        }
    }
    
    public IEnumerator PopulateCharacters()
    {
        if (!m_IsLoadingCharacter)
        {
            m_IsLoadingCharacter = true;
            GameObject newChar = null;
            while (newChar == null)
            {
                Character c = CharacterDatabase.GetCharacter(PlayerData.instance.character);

                if (c != null)
                {
                    Vector3 pos = charPosition.transform.position;
                    pos.x = 0.0f;
                    charPosition.transform.position = pos;

                    AsyncOperationHandle op = Addressables.InstantiateAsync(c.characterName);
                    yield return op;
                    if (op.Result == null || !(op.Result is GameObject))
                    {
                        Debug.LogWarning(string.Format("Unable to load character {0}.", c.characterName));
                        yield break;
                    }
                    newChar = op.Result as GameObject;
                    Helpers.SetRendererLayerRecursive(newChar, k_UILayer);
					newChar.transform.SetParent(charPosition, false);
                    newChar.transform.rotation = k_FlippedYAxisRotation;

                    if (m_Character != null)
                        Addressables.ReleaseInstance(m_Character);

                    m_Character = newChar;

                    m_Character.transform.localPosition = Vector3.right * 1000;
                    yield return new WaitForEndOfFrame();
                    m_Character.transform.localPosition = Vector3.zero;
                }
                else
                    yield return new WaitForSeconds(1.0f);
            }
            m_IsLoadingCharacter = false;
        }
	}
	

	public void SetModifier(Modifier modifier)
	{
		m_CurrentModifier = modifier;
	}

    public void StartGame()
    {
        manager.SwitchState("Game");
    }
}

using UnityEngine;
using System.IO;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class PlayerData
{
	static protected PlayerData m_Instance;
	static public PlayerData instance { get { return m_Instance; } }

	protected string saveFile = "";

	public string character = "Trash Cat";
	public string theme = "Day";

	public int coins;
	public float masterVolume = float.MinValue, musicVolume = float.MinValue, masterSFXVolume = float.MinValue;

	static int s_Version = 12;

	// File management

	static public void Create()
	{
		if (m_Instance == null)
		{
			m_Instance = new PlayerData();

			CoroutineHandler.StartStaticCoroutine(CharacterDatabase.LoadDatabase());
			CoroutineHandler.StartStaticCoroutine(ThemeDatabase.LoadDatabase());
		}

		m_Instance.saveFile = Application.persistentDataPath + "/save.bin";

		if (File.Exists(m_Instance.saveFile))
		{
			m_Instance.Read();
		}
		else
		{
			NewSave();
		}
	}

	static public void NewSave()
	{
		m_Instance.character = "Trash Cat";
		m_Instance.theme = "Day";
		m_Instance.coins = 0;
		m_Instance.masterVolume = 0.0f;
		m_Instance.musicVolume = 0.0f;
		m_Instance.masterSFXVolume = 0.0f;

		m_Instance.Save();
	}

	public void Read()
	{
		BinaryReader r = new BinaryReader(new FileStream(saveFile, FileMode.Open));

		int ver = r.ReadInt32();

		if (ver < 6)
		{
			r.Close();

			NewSave();
			r = new BinaryReader(new FileStream(saveFile, FileMode.Open));
			ver = r.ReadInt32();
		}

		coins = r.ReadInt32();

		if (ver >= 9)
		{
			masterVolume = r.ReadSingle();
			musicVolume = r.ReadSingle();
			masterSFXVolume = r.ReadSingle();
		}

		r.Close();
	}

	public void Save()
	{
		BinaryWriter w = new BinaryWriter(new FileStream(saveFile, FileMode.OpenOrCreate));

		w.Write(s_Version);
		w.Write(coins);

		w.Write(masterVolume);
		w.Write(musicVolume);
		w.Write(masterSFXVolume);

		w.Close();
	}


}
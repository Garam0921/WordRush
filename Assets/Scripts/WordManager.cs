using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using JetBrains.Annotations;

public class WordManager : MonoBehaviour
{
	static public WordManager Instance { get { return s_Instance; } }
	static protected WordManager s_Instance;

	Dictionary<string, List<Dictionary<string, string>>> words = null;
	Dictionary<string, Queue<Dictionary<string, string>>> wordPool = null;   // <page, word>

	public void OnEnable()
	{
		s_Instance = this;

		words = ExcelReader.GetExcelData("words");
		wordPool = new Dictionary<string, Queue<Dictionary<string, string>>>();
	}

	/// <summary>
	/// 기초, 800점, 900점
	/// </summary>
	public List<Dictionary<string, string>> GetWords(string page)
	{
		return words[page];
	}

	public Dictionary<string, string> GetRandomWordSet(string page)
	{
		if (!wordPool.ContainsKey(page))
			wordPool.Add(page, new Queue<Dictionary<string, string>>());

		if (wordPool[page].Count <= 0)
		{
			List<Dictionary<string, string>> list = GetWords(page);
			System.Random rand = new System.Random();
			var shuffled = list.OrderBy(_ => rand.Next()).ToList();

			for (int i = 0; i < shuffled.Count; i++)
				wordPool[page].Enqueue(shuffled[i]);
		}

		return wordPool[page].Dequeue();
	}
}

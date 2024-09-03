using System.IO;
using UnityEngine;
using ExcelDataReader;
using System.Collections.Generic;
using System.Data;
using static UnityEngine.Rendering.DebugUI.Table;
using System;

public class ExcelReader
{
	public static Dictionary<string, List<Dictionary<string, string>>> GetExcelData(string fileName)
	{
		string filePath = Application.dataPath + "/Resources/" + fileName + ".xlsx";
		Dictionary<string, List<Dictionary<string, string>>> data = new Dictionary<string, List<Dictionary<string, string>>>();

		using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
		{
			using (var reader = ExcelReaderFactory.CreateReader(stream))
			{
				var result = reader.AsDataSet();
				for (int i = 0; i < result.Tables.Count; i++)
				{
					DataTable table = result.Tables[i];
					data.Add(table.TableName, new());

					if (table.Rows.Count <= 0)
						continue;

					List<string> keys = new List<string>();
					for (int k = 0; k < table.Rows[0].ItemArray.Length; k++)
						keys.Add(table.Rows[0][k].ToString());

					for (int j = 1; j < table.Rows.Count; j++)
					{
						Dictionary<string, string> dict = new Dictionary<string, string>();

						for (int k = 0; k < keys.Count; k++)
							dict.Add(keys[k], table.Rows[j][k].ToString());

						data[table.TableName].Add(dict);
					}
				}
			}
		}
		return data;
	}
}

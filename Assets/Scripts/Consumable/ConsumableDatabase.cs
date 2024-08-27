using UnityEngine;
using System.Collections.Generic;
 
[CreateAssetMenu(fileName="Consumables", menuName = "Trash Dash/Consumables Database")]
public class ConsumableDatabase : ScriptableObject
{
    public Consumable[] consumbales;

    static protected Dictionary<Consumable.ConsumableType, Consumable> _consumablesDict;

    public void Load()
    {
        if (_consumablesDict == null)
        {
            _consumablesDict = new Dictionary<Consumable.ConsumableType, Consumable>();

            for (int i = 0; i < consumbales.Length; ++i)
            {
                _consumablesDict.Add(consumbales[i].GetConsumableType(), consumbales[i]);
            }
        }
    }

    static public Consumable GetConsumbale(Consumable.ConsumableType type)
    {
        Consumable c;
        return _consumablesDict.TryGetValue (type, out c) ? c : null;
    }
}

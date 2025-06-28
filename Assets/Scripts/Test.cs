using UnityEngine;

public class Test : MonoBehaviour
{
#if UNITY_NETCODE_GAMEOBJECTS
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Debug.Log("Kazkas");
    }
#endif
    // Update is called once per frame
    void Update()
    {
        
    }
}

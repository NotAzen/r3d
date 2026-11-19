using UnityEngine;

public class healthyCode : MonoBehaviour
{
    public Transform lcp;
    public GameObject player;
    

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
       
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    private void OnTriggerEnter2D(UnityEngine.Collider2D col)
    {
        if (col.gameObject.CompareTag("point"))
        {
            lcp.position = col.transform.position;
            Debug.Log("log");
        }
    }
}

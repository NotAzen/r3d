using UnityEngine;

public class healthyCode : MonoBehaviour
{
    public Transform lcp;
    public GameObject player;

    public int health = 3;

    void Update()
    {
        if(health <= 0)
        {
            Destroy(this.gameObject);
        }

        
    }
    private void OnTriggerEnter2D(UnityEngine.Collider2D col)
    {
        if (col.gameObject.CompareTag("point"))
        {
            lcp.position = col.transform.position;
            Debug.Log("log");
        }
       
    }
public void OnCollisionEnter2D(Collision2D ol)
    {
       
        if (ol.gameObject.CompareTag("hazard"))
        {
            health--;
            Debug.Log("log");
        }
    }
}

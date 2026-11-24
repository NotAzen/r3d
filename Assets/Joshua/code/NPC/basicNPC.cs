using NUnit.Framework;
using UnityEngine;

public class basicNPC : MonoBehaviour
{

    public CircleCollider2D cc2D;
    public bool istalking;
    
    
    void Start()
    {
        cc2D = GetComponent<CircleCollider2D>();
    }


    void Update()
    {

    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        float talkChance = Random.Range(0, 12);
        if (talkChance >= 6) {
            Debug.Log("talking");
            istalking = true;
        }
        else
        {
            Debug.Log("Failed to start convo");
            istalking = false;
        }

    }
    private void OnTriggerExit2D(Collider2D collision)
    {
        Debug.Log("doned talking");
    }
}

using UnityEngine;

public class DangerousTile : MonoBehaviour
{
    public healthyCode hc;

    private void Start()
    {
        GetComponent<healthyCode>();
    }
    private void OnCollisionEnter2D(Collision2D other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            hc.player.transform.position = hc.lcp.position;
        }
    }
}

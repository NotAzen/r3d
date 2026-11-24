using System.Collections;
using Unity.VisualScripting;
using UnityEditor.PackageManager;
using UnityEngine;

public class DangerousTile : MonoBehaviour
{
   
    public healthyCode hc;
    private SpriteRenderer sr;

    [Header("Sometimes on")]
    public bool doesIntervols;
    public float intervolRate;
    public float delay;
    public float AT;
    public Collider2D cr;


    void Start()
    {
        sr = GetComponent<SpriteRenderer>();
        cr = GetComponent<Collider2D>();
        hc = FindAnyObjectByType<healthyCode>();
        if (doesIntervols)
        {
            StartCoroutine(Damaging());
        }

    }

    private void Update()
    {
      
    }
    private void OnCollisionEnter2D(Collision2D other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            hc.player.transform.position = hc.lcp.position;
        }
    }
    IEnumerator Damaging()
    {
        cr.enabled = false;
        sr.enabled = true;
        sr.color = Color.yellow;
      
        yield return new WaitForSeconds(delay);

        sr.color = Color.red;
        sr.enabled = true;
        cr.enabled = true;
        sr.enabled = true; 

        yield return new WaitForSeconds(AT);

        cr.enabled = false;
        sr.enabled = false;


        yield return new WaitForSeconds(intervolRate);
         sr.enabled = true;
        StartCoroutine(Damaging());
       
    }
}

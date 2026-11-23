using UnityEngine;

public class collectables : MonoBehaviour
{
    private PlayerMovement PM;
    public Locks Objname;

    void Awake()
    {
        PM = GameObject.FindGameObjectWithTag("Player").GetComponent<PlayerMovement>(); // automatic assignment
    }

    public void OnTriggerEnter2D(UnityEngine.Collider2D collision)
    {
        PM.UnlockAbility(Objname);
        Destroy(this.gameObject ,0.2f);
    }
}

using UnityEngine;

public class collectables : MonoBehaviour
{
    public PlayerMovement PM;
    public Locks Objname;

    public void OnTriggerEnter2D(UnityEngine.Collider2D collision)
    {
        PM.UnlockAbility(Objname);
        Destroy(this.gameObject ,0.2f);
    }
}

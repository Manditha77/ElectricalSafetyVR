using UnityEngine;

public class SessionButtons : MonoBehaviour
{
    public void StartTraining()
    {
        SessionManager.Instance.BeginTraining();
    }

    public void Restart()
    {
        SessionManager.Instance.Restart();
    }
}
using UnityEngine;

public class CreateGameScreen : MonoBehaviour
{
    public void PhotonJoinGame()
    {
        PhotonManager.Instance.JoinGame();
    }

    public void PhotonCreateGame()
    {
        PhotonManager.Instance.StartGame();
        
    }
}

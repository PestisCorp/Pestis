using Fusion;
using Networking;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class StartMenu : MonoBehaviour
    {
        public NetworkRunner runner;
        public Button joinButton;
        public TMP_Text usernameInput;

        public async void OnJoin()
        {
            GameManager.Instance.localUsername = usernameInput.text;
            await SessionManager.Instance.JoinGame(runner);
            gameObject.SetActive(false);
        }
    }
}
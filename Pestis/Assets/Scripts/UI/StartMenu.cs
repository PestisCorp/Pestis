using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace UI
{
    public class StartMenu : MonoBehaviour
    {
        public NetworkRunner runner;
        public Button joinButton;
        public TMP_Text usernameInput;

        public void OnJoin()
        {
            GameManager.Instance.localUsername = usernameInput.text;
            var args = new StartGameArgs();
            args.GameMode = GameMode.Shared;
            var scene = new NetworkSceneInfo();
            scene.AddSceneRef(SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex));
            args.Scene = scene;
            runner.StartGame(args);

            gameObject.SetActive(false);
        }
    }
}
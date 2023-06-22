﻿using Comfort.Common;
using EFT;
using EFT.CameraControl;
using EFT.UI;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace SIT.Core.Coop.FreeCamera
{
    /// <summary>
    /// This is HEAVILY based on Terkoiz's work found here. Thanks for your work Terkoiz! 
    /// https://dev.sp-tarkov.com/Terkoiz/Freecam/raw/branch/master/project/Terkoiz.Freecam/FreecamController.cs
    /// </summary>

    public class FreeCameraController : MonoBehaviour
    {
        private GameObject _mainCamera;
        private FreeCamera _freeCamScript;

        private BattleUIScreen _playerUi;
        private bool _uiHidden;

        private GamePlayerOwner _gamePlayerOwner;

        private Vector3? _lastPosition;
        private Quaternion? _lastRotation;


        public void Start()
        {
            // Find Main Camera
            _mainCamera = GameObject.Find("FPS Camera");
            if (_mainCamera == null)
            {
                return;
            }

            // Add Freecam script to main camera in scene
            _freeCamScript = _mainCamera.AddComponent<FreeCamera>();
            if (_freeCamScript == null)
            {
                return;
            }

            // Get GamePlayerOwner component
            _gamePlayerOwner = GetLocalPlayerFromWorld().GetComponentInChildren<GamePlayerOwner>();
            if (_gamePlayerOwner == null)
            {
                return;
            }
        }

        private DateTime _lastTime = DateTime.MinValue;

        public void Update()
        {
            if (_gamePlayerOwner == null)
                return;

            if (_gamePlayerOwner.Player == null)
                return;

            if (_gamePlayerOwner.Player.PlayerHealthController == null)
                return;


            if (Input.GetKey(KeyCode.F9) 
                || (!_gamePlayerOwner.Player.PlayerHealthController.IsAlive && !_freeCamScript.IsActive)
                && _lastTime < DateTime.Now.AddSeconds(-3)
            )
            {
                _lastTime = DateTime.Now;
                ToggleCamera();
                ToggleUi();
            }

        }

        /// <summary>
        /// Toggles the Freecam mode
        /// </summary>
        public void ToggleCamera()
        {
            // Get our own Player instance. Null means we're not in a raid
            var localPlayer = GetLocalPlayerFromWorld();
            if (localPlayer == null)
                return;

            if (!_freeCamScript.IsActive)
            {
                SetPlayerToFreecamMode(localPlayer);
            }
            else
            {
                SetPlayerToFirstPersonMode(localPlayer);
            }
        }

        /// <summary>
        /// When triggered during Freecam mode, teleports the player to where the camera was and exits Freecam mode
        /// </summary>
        private void MovePlayerToCamera()
        {
            var localPlayer = GetLocalPlayerFromWorld();
            if (localPlayer == null)
                return;

            // Move the player to the camera's current position and switch to First Person mode
            if (_freeCamScript.IsActive)
            {
                // We grab the camera's position, but we subtract a bit off the Y axis, because the players coordinate origin is at the feet
                var position = new Vector3(_mainCamera.transform.position.x, _mainCamera.transform.position.y - 1.8f, _mainCamera.transform.position.z);
                localPlayer.gameObject.transform.SetPositionAndRotation(position, Quaternion.Euler(0, _mainCamera.transform.rotation.y, 0));

                // localPlayer.gameObject.transform.SetPositionAndRotation(position, _mainCamera.transform.rotation);
                SetPlayerToFirstPersonMode(localPlayer);
            }
        }

        /// <summary>
        /// Hides the main UI (health, stamina, stance, hotbar, etc.)
        /// </summary>
        private void ToggleUi()
        {
            // Check if we're currently in a raid
            if (GetLocalPlayerFromWorld() == null)
                return;

            // If we don't have the UI Component cached, go look for it in the scene
            if (_playerUi == null)
            {
                _playerUi = GameObject.Find("BattleUIScreen").GetComponent<BattleUIScreen>();

                if (_playerUi == null)
                {
                    //FreecamPlugin.Logger.LogError("Failed to locate player UI");
                    return;
                }
            }

            _playerUi.gameObject.SetActive(_uiHidden);
            _uiHidden = !_uiHidden;
        }

        /// <summary>
        /// A helper method to set the Player into Freecam mode
        /// </summary>
        /// <param name="localPlayer"></param>
        private void SetPlayerToFreecamMode(EFT.Player localPlayer)
        {
            // We set the player to third person mode, but then we want set the camera to freecam mode
            // This means our character will be fully visible, while letting the camera move freely
            // Setting the player point of view directly to Freecam seems to hide the head and arms of the character, which is not desirable

            localPlayer.PointOfView = EPointOfView.ThirdPerson;

            // Get the PlayerBody reference. It's a protected field, so we have to use traverse to fetch it
            var playerBody = Traverse.Create(localPlayer).Field<PlayerBody>("_playerBody").Value;
            if (playerBody != null)
            {
                // We tell the PlayerBody class that it's in FreeCamera mode, and force an update of the Camera Controller view mode
                // Setting the PointOfView.Value directly skips all the code that would usually change how the player body is rendered
                playerBody.PointOfView.Value = EPointOfView.FreeCamera;
                localPlayer.GetComponent<PlayerCameraController>().UpdatePointOfView();

                // All we really needed to do, was trigger the UpdatePointOfView method and have it update to the FreeCam state
                // There's no easy way of doing this without patching the method, and even then it might be a bloated solution
            }
            else
            {
                //FreecamPlugin.Logger.LogError("Failed to get the PlayerBody field");
            }

            // Instead of Detouring, just turn off _gamePlayerOwner which takes the input
            _gamePlayerOwner.enabled = false;

            //if (FreecamPlugin.CameraRememberLastPosition.Value && _lastPosition != null && _lastRotation != null)
            //{
            //    _mainCamera.transform.position = _lastPosition.Value;
            //    _mainCamera.transform.rotation = _lastRotation.Value;
            //}

            _freeCamScript.IsActive = true;
        }

        /// <summary>
        /// A helper method to reset the player view back to First Person
        /// </summary>
        /// <param name="localPlayer"></param>
        private void SetPlayerToFirstPersonMode(EFT.Player localPlayer)
        {
            _freeCamScript.IsActive = false;

            //if (FreecamPlugin.CameraRememberLastPosition.Value)
            //{
            //    _lastPosition = _mainCamera.transform.position;
            //    _lastRotation = _mainCamera.transform.rotation;
            //}

            // re-enable _gamePlayerOwner
            _gamePlayerOwner.enabled = true;

            localPlayer.PointOfView = EPointOfView.FirstPerson;
        }

        /// <summary>
        /// Gets the current <see cref="Player"/> instance if it's available
        /// </summary>
        /// <returns>Local <see cref="Player"/> instance; returns null if the game is not in raid</returns>
        private EFT.Player GetLocalPlayerFromWorld()
        {
            // If the GameWorld instance is null or has no RegisteredPlayers, it most likely means we're not in a raid
            var gameWorld = Singleton<GameWorld>.Instance;
            if (gameWorld == null || gameWorld.MainPlayer == null)
                return null;

            // One of the RegisteredPlayers will have the IsYourPlayer flag set, which will be our own Player instance
            return gameWorld.MainPlayer;
        }

        public void OnDestroy()
        {
            // Destroy FreeCamScript before FreeCamController if exists
            Destroy(_freeCamScript);
            Destroy(this);
        }
    }
}
using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class InputHandler : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] PlayerController playerController;
    [SerializeField] AnimatorHandler animatorHandler;
    [SerializeField] WeaponHandler weaponHandler;
    [SerializeField] GameObject cameraHolder;
    [SerializeField] PhotonView PV;

    public bool isAiming = false;

    [HideInInspector] public float vert;
    [HideInInspector] public float horz;
    [HideInInspector] public float MouseX;
    [HideInInspector] public float MouseY;
    [HideInInspector] public bool isPaused = false;
    float verticalLookRotation;
    private void Look()
    {
        if (isPaused) return;
        float mouseXSensitivity = PlayerPrefs.GetFloat("MouseXSensitivity");
        float mouseYSensitivity = PlayerPrefs.GetFloat("MouseYSensitivity");
        verticalLookRotation += MouseY * mouseYSensitivity;
        verticalLookRotation = Mathf.Clamp(verticalLookRotation, -90, 90);


        transform.Rotate(Vector3.up * MouseX * mouseXSensitivity);
        cameraHolder.transform.localEulerAngles = Vector3.left * verticalLookRotation;

    }

    private void HandleInput()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            GameManager.instance.OpenInGameMenu(this);
        }
        if (isPaused) return;
        vert = Input.GetAxisRaw("Vertical");
        horz = Input.GetAxisRaw("Horizontal");

        MouseX = Input.GetAxisRaw("Mouse X");
        MouseY = Input.GetAxisRaw("Mouse Y");


        for (int i = 0; i < weaponHandler.guns.Length; i++)
        {
            
            if (Input.GetKeyDown((i + 1).ToString()) && !weaponHandler.isReloading)
            {
                weaponHandler.Equip(i);
                break;
            }
        }
        if (Input.GetMouseButton(0) && !weaponHandler.isReloading)
        {
            weaponHandler.Shoot();
        }
        if(Input.GetMouseButtonDown(1) && !weaponHandler.isReloading)
        {
            weaponHandler.AimDownSights();
        }
        if (Input.GetKeyDown(KeyCode.R) && !weaponHandler.isReloading && weaponHandler.currentGun.currentAmmo != weaponHandler.currentGun.ammoPerMag)
        {
            string state = animatorHandler.GetCurrentGunAnimation();
            if (!state.Contains("Idle")) return;
            weaponHandler.Reload();
        }
    }

    private void Update()
    {
        if (!PV.IsMine) return;
        HandleInput();
        Look();
    }

}

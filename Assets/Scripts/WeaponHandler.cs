using Photon.Pun;
using Photon.Realtime;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static UnityEngine.GraphicsBuffer;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class WeaponHandler : MonoBehaviour
{
    [Header("Refs")]
    public AnimatorHandler animatorHandler;
    public PlayerController playerController;
    public RecoilHandler recoilHandler;
    public TMP_Text ammoText;
    public TMP_Text reloadText;
    public Image crosshair;
    public Camera cam;
    [SerializeField] AudioSource weaponSource;
    public GameObject[] impacts;
    public PhotonView PV;
    public ObjectPool objectPool;
    public ObjectPool muzzleFlashesPool;

    [HideInInspector] public Animator weaponAnimator;

    [Header("Guns")]
    public GameObject[] fpsGuns;
    public GameObject[] tpsGuns;
    public Gun[] guns;

    float nextShootTimer;

    [HideInInspector] public Gun currentGun;
    int previousItemIndex = -1;
    int gunIndex;
    Transform currentGunTranform;


    public bool isAiming = false;
    public bool isReloading = false;

    // The starting field of view
    public float startingFOV = 75.0f;

    // The target field of view
    public float aimFov = 65.0f;

    // The speed of the FOV transition
    public float transitionSpeed = 1.0f;

    // The progress of the FOV transition (0-1)
    private float transitionProgress = 0.0f;


    private void Awake()
    {
        foreach (Gun gun in guns)
        {
            gun.currentAmmo = gun.ammoPerMag;
        }
    }
    private void Start()
    {
        if (PV.IsMine)
        {
            PV.RPC("RPC_Equip", RpcTarget.All, 0);
            // Set the starting field of view
            cam.fieldOfView = startingFOV;
        }
    }
    private void Update()
    {
        // Increase the transition progress
        transitionProgress += Time.deltaTime * transitionSpeed;

        // Clamp the transition progress between 0 and 1
        transitionProgress = Mathf.Clamp01(transitionProgress);


    }
    public void AimDownSights()
    {
        if (currentGunTranform == null) return;
        if(isAiming)
        {
            isAiming = false;
            crosshair.enabled = true;
            currentGunTranform.localPosition = currentGun.Position;
            currentGunTranform.localRotation = currentGun.Rotation;
            // Lerp between the starting and target FOV
            cam.fieldOfView = Mathf.Lerp(aimFov, startingFOV, transitionProgress);
        }
        else if (!isAiming)
        {
            isAiming = true;
            crosshair.enabled = false;
            currentGunTranform.localPosition = currentGun.ADS_Position;
            currentGunTranform.localRotation = currentGun.ADS_Rotation;
            cam.fieldOfView = Mathf.Lerp(startingFOV, aimFov, transitionProgress);
        }
    }
    public void Equip(int _index)
    {
        PV.RPC("RPC_Equip", RpcTarget.All, _index);
    }
    [PunRPC]
    private void RPC_Equip(int _index)
    {
        if (_index == previousItemIndex) return;
        gunIndex = _index;
        currentGun = guns[gunIndex];
        if(currentGun.weaponType == WeaponType.Primary)
        {
            animatorHandler.UpdateAnimatorFloat("gunIndex", 0f);
        }
        else if(currentGun.weaponType == WeaponType.Secondary)
        {
            animatorHandler.UpdateAnimatorFloat("gunIndex", 1f);
        }
        else if(currentGun.weaponType == WeaponType.Knife)
        {
            animatorHandler.UpdateAnimatorFloat("gunIndex", 2f);

        }
        previousItemIndex = gunIndex;
        isAiming = false;
        ammoText.text = currentGun.currentAmmo.ToString() + "/" + currentGun.ammoLeft;
        foreach (GameObject go in tpsGuns)
        {
            if (go.transform.name != currentGun.name +"_TP")
            {
                go.SetActive(false);
            }
            else
            {
                go.SetActive(true);
            }
        }
        if (PV.IsMine)
        {
            if(currentGun.weaponType == WeaponType.Knife)
            {
                playerController.sprintSpeed = 14f;
            }
            else
            {
                playerController.sprintSpeed = 6f;
            }
            foreach (GameObject go in fpsGuns)
            {
                if (go.transform.name != currentGun.name)
                {
                    go.SetActive(false);
                }
                else
                {
                    go.SetActive(true);
                    weaponAnimator = go.transform.GetComponent<Animator>();
                    currentGunTranform = go.transform;
                    currentGunTranform.localPosition = currentGun.Position;
                    currentGunTranform.localRotation = currentGun.Rotation;
                }
            }            
        }
    }
    public void Shoot()
    {
        if (nextShootTimer > Time.time) return;
        if (currentGun.currentAmmo <= 0) return;
        if (PV.IsMine)
        {
            if(weaponAnimator != null)
            {
                weaponAnimator.CrossFadeInFixedTime("Shoot", 0.01f);

            }
            else
            {
                foreach (GameObject go in fpsGuns)
                {
                    if (go.transform.name == currentGun.name)
                    {
                        weaponAnimator = go.transform.GetComponent<Animator>();
                        weaponAnimator.CrossFadeInFixedTime("Shoot", 0.01f);
                    }
                }
            }
        }
        if(currentGun.weaponType != WeaponType.Knife)
        {

            HandleBulletSpread();
            RaycastHit hit;
            for (int i = 0; i < Mathf.Max(1, currentGun.pellets); i++)
            {
                Vector3 t_spread = cam.transform.position + cam.transform.forward * 1000f;
                t_spread += Random.Range(-currentGun.bulletSpread, currentGun.bulletSpread) * cam.transform.up;
                t_spread += Random.Range(-currentGun.bulletSpread, currentGun.bulletSpread) * cam.transform.right;
                t_spread -= cam.transform.position;
                t_spread.Normalize();

                if (Physics.Raycast(cam.transform.position, t_spread, out hit, currentGun.weaponRange))
                {
                    Vector3 hitNormal = hit.normal;
                    if (hit.collider.gameObject.GetComponent<PlayerController>())
                    { // Friendly Fire
                        hit.collider.gameObject.GetComponent<IDamagable>()?.TakeDamage(currentGun.damage);
                        PV.RPC("RPC_BulletImpact", RpcTarget.All, hit.point, hitNormal, "Enemy");
                        playerController.PlayHitSound();
                    }
                    else
                    {
                        PV.RPC("RPC_BulletImpact", RpcTarget.All, hit.point, hitNormal, hit.transform.gameObject.tag);
                    }
                }
            }
            // Weapon Kickback
            recoilHandler.Fire();
            // Camera Recoil (Actual Recoil)

            currentGun.currentAmmo--;
            ammoText.text = currentGun.currentAmmo.ToString() + "/" + currentGun.ammoLeft;
            PV.RPC("RPC_MuzzleFlash", RpcTarget.All);
        }
        else if(currentGun.weaponType == WeaponType.Knife)
        {
            RaycastHit hit;
            if (Physics.Raycast(cam.transform.position, transform.forward, out hit, currentGun.weaponRange))
            {
                Vector3 hitNormal = hit.normal;
                if (hit.collider.gameObject.GetComponent<PlayerController>())
                { // Friendly Fire
                    hit.collider.gameObject.GetComponent<IDamagable>()?.TakeDamage(currentGun.damage);
                    PV.RPC("RPC_BulletImpact", RpcTarget.All, hit.point, hitNormal, "Enemy");
                    playerController.PlayHitSound();
                }
                else
                {
                    PV.RPC("RPC_BulletImpact", RpcTarget.All, hit.point, hitNormal, hit.transform.gameObject.tag);
                }
            }
        }
            PV.RPC("RPC_ShootSound", RpcTarget.All);
            animatorHandler.CrossFadeInFixedTime("Shoot", 0.01f);
            nextShootTimer = Time.time + currentGun.fireRate;
    }

    [PunRPC]
    void RPC_MuzzleFlash()
    {
        GameObject currentGun_TP = null;
        foreach (GameObject go in tpsGuns)
        {
            if (go.transform.name == currentGun.name + "_TP")
            {
                currentGun_TP = go;
            }
        }
        GameObject muzzleFlash = objectPool.GetPooledObject(1);
        if (muzzleFlash != null)
        {
            muzzleFlash.transform.position = currentGun_TP.transform.Find("MuzzleFlash").position;
            muzzleFlash.transform.rotation = currentGun_TP.transform.Find("MuzzleFlash").rotation;
            muzzleFlash.SetActive(true);
        }
        StartCoroutine(DisableBulletImpacts(muzzleFlash));

    }
    public void Reload()
    {
        PV.RPC("RPC_Reload", RpcTarget.All);
    }
    [PunRPC]
    IEnumerator RPC_Reload()
    {
        if (PV.IsMine)
        {
            isReloading = true;
            weaponAnimator.CrossFadeInFixedTime("Reload", 0.01f);
            weaponAnimator.SetBool("Reload", true);
            reloadText.gameObject.SetActive(true);
            yield return new WaitForSeconds(currentGun.reloadDuration);
            isReloading = false;
            weaponAnimator.SetBool("Reload", false);
            reloadText.gameObject.SetActive(false);
            int reloadAmount = currentGun.ammoPerMag - currentGun.currentAmmo;
            currentGun.currentAmmo = currentGun.ammoPerMag;
            currentGun.ammoLeft -= reloadAmount;
            ammoText.text = currentGun.currentAmmo.ToString() + "/" + currentGun.ammoLeft;
        }
    }
    [PunRPC]
    void RPC_BulletImpact(Vector3 hitPosition, Vector3 hitNormal, string hitType)
    {
        GameObject bulletImpact = objectPool.GetPooledObject(0);
        if (bulletImpact != null)
        {
            bulletImpact.transform.position = hitPosition;
            bulletImpact.transform.rotation = Quaternion.LookRotation(hitNormal);
            bulletImpact.SetActive(true);
        }
        StartCoroutine(DisableBulletImpacts(bulletImpact));
    }
    IEnumerator DisableBulletImpacts(GameObject go)
    {
        yield return new WaitForSeconds(1);
        go.SetActive(false);
    }
    [PunRPC]
    void RPC_ShootSound()
    {
        weaponSource.PlayOneShot(currentGun.shootSound);
        weaponSource.volume = currentGun.shootVolume;
    }
    public void HandleBulletSpread()
    {
        if (playerController.isMoving)
        {
            foreach (GameObject go in fpsGuns)
            {
                if (go.transform.name == currentGun.name)
                {
                    currentGun.bulletSpread = currentGun.runningBulletSpread;
                }
            }
        }
        else
        {
            isAiming = true;
            foreach (GameObject go in fpsGuns)
            {
                if (go.transform.name == currentGun.name)
                {
                    currentGun.bulletSpread = currentGun.normalBulletSpread;
                }
            }
        }

    }

}

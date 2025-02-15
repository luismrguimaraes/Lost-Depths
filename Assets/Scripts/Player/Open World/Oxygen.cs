using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;


public class Oxygen : MonoBehaviour
{
    public float oxygenValue;
    //public Slider oxygenSlider;
    //public TMP_Text oxygenText;

    public GameObject statusBar;

    private Vector3 lastPosition;
    private Vector3 oxygenStationPosition;
    private float oxygenStationRot;
    private float oxygenLostSpeed = 2f;
    private float oxygenRefillSpeed = 50f;
    private bool refilling = false;

    public float maxOxygen;

    CharacterController controller;
    PlayerMovement movement;

    public FMODUnity.EventReference outOfOxygenEvent;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        movement = GetComponent<PlayerMovement>();
        lastPosition = transform.position;
        oxygenStationPosition = transform.position;
        //oxygenSlider.maxValue = 300;

        statusBar.GetComponent<StatusBarScript>().UpdateFilledAmount(oxygenValue / maxOxygen);
    }

    void Update()
    {
        float distanceMoved = Vector3.Distance(transform.position, lastPosition);
        lastPosition = transform.position;
        if (distanceMoved < 1 && !refilling)
        {
            oxygenValue -= distanceMoved * oxygenLostSpeed;
        }

        if (oxygenValue <= 0)
        {
            oxygenValue = 0;
            Die();
        }

        statusBar.GetComponent<StatusBarScript>().UpdateFilledAmount(oxygenValue / maxOxygen);
        float valueForFMOD = oxygenValue / maxOxygen;
        FMODUnity.RuntimeManager.StudioSystem.setParameterByName("OxygenValue", valueForFMOD);
        //print(valueForFMOD);
        //UpdateSlider(oxygenValue);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("OxygenStation"))
        {
            oxygenStationPosition = other.transform.parent.transform.position;
            oxygenStationPosition += new Vector3(0, 1, 0);
            oxygenStationRot = other.GetComponentInParent<RaiseWater>().y_Rot;
            StoreCheckpoint();
            Debug.Log("saving checkpoint");
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (other.CompareTag("OxygenStation"))
        {
            RefillOxygen(); 
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("OxygenStation"))
        {
            refilling = false;
        }
    }

    //void UpdateSlider(float value)
    //{
    //    value = Mathf.Clamp(value, 0f, oxygenSlider.maxValue);

    //    oxygenSlider.value = value;
    //    oxygenText.text = "Water: " + value.ToString("F0");

    //    float valueForFMOD = value / oxygenSlider.maxValue;
    //    FMODUnity.RuntimeManager.StudioSystem.setParameterByName("OxygenValue", valueForFMOD);
    //}

    public void SetOxygenValue(float oxygenValue)
    {
        this.oxygenValue = oxygenValue;
        //this.UpdateSlider(oxygenValue);
        statusBar.GetComponent<StatusBarScript>().UpdateFilledAmount(oxygenValue / maxOxygen);

    }

    void RefillOxygen()
    {
        if(oxygenValue < maxOxygen)
        {
            refilling = true;
            oxygenValue += oxygenRefillSpeed * Time.deltaTime;
            statusBar.GetComponent<StatusBarScript>().UpdateFilledAmount(oxygenValue / maxOxygen);
            //UpdateSlider(oxygenValue);

        }
    }


    void Die()
    {
        Debug.Log("Player has run out of oxygen");

        // Play FMOD event
        FMODUnity.RuntimeManager.PlayOneShotAttached(outOfOxygenEvent, gameObject);

        ResetPos();
    }

    void ResetPos() {
        movement.ResetMovement();

        // Implement restart the level
        controller.enabled = false;
        transform.position = oxygenStationPosition;
        transform.rotation = Quaternion.Euler(0, oxygenStationRot, 0);
        controller.enabled = true;
        oxygenValue = maxOxygen;

        statusBar.GetComponent<StatusBarScript>().UpdateFilledAmount(oxygenValue / maxOxygen);

        //UpdateSlider(oxygenValue);
    }

    void StoreCheckpoint()
    {
        print("Storing");
        PlayerPrefs.SetFloat("CheckpointX", oxygenStationPosition.x);
        PlayerPrefs.SetFloat("CheckpointY", oxygenStationPosition.y);
        PlayerPrefs.SetFloat("CheckpointZ", oxygenStationPosition.z);
        PlayerPrefs.SetFloat("CheckpointRot", oxygenStationRot);
    }

    public void LoadCheckpoint()
    {
        if (!HasCheckpoint())
            return;
        oxygenStationPosition = GetCheckpoint();
        oxygenStationRot = GetRot();
        ResetPos();
    }

    public bool HasCheckpoint()
    {
        return PlayerPrefs.HasKey("CheckpointX");
    }

    public Vector3 GetCheckpoint()
    {
        return new Vector3(PlayerPrefs.GetFloat("CheckpointX"), PlayerPrefs.GetFloat("CheckpointY"), PlayerPrefs.GetFloat("CheckpointZ"));
    }

    public float GetRot()
    {
        return PlayerPrefs.GetFloat("CheckpointRot");
    }
}

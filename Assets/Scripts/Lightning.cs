using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Lightning : MonoBehaviour
{
    [SerializeField]
    private GameObject LightningONE;
    [SerializeField]
    private GameObject LightningTWO;
    [SerializeField]
    private GameObject LightningTHREE;
    [SerializeField]
    private GameObject thunder1;
    [SerializeField]
    private GameObject thunder2;
    [SerializeField]
    private GameObject thunder3;
    
    void Start()
    {
        //ensuring lighting is off
        LightningONE.SetActive(false);
        LightningTWO.SetActive(false);
        LightningTHREE.SetActive(false);


        //ensuring thunder is off
        thunder1.SetActive(false);
        thunder2.SetActive(false);
        thunder3.SetActive(false);
        
        Invoke("callLightning", 5f);
    }
    void callLightning() 
    {
        int r = Random.Range(0, 3);
       
        if(r == 0)
        {
            LightningONE.SetActive(true);
            Invoke("endLightning", 0.75f);
            Invoke("callThunder1", 1f);
           
        }
        if (r==1)
        {
            LightningTWO.SetActive(true);
            Invoke("endLightning", 0.75f);
            Invoke("callThunder2", 1.75f);
         
        }

        if (r==2)
        {
            LightningTHREE.SetActive(true);
            Invoke("endLightning", 0.75f);
            Invoke("callThunder3", 1.25f);
        }
        if (r == 3)
        {
            LightningTHREE.SetActive(true);
            Invoke("endLightning", 0.75f);
            Invoke("callThunder3", 1.25f);
        }
    }

    void endLightning() 
    {
        LightningONE.SetActive(false);
        LightningTWO.SetActive(false);
        LightningTHREE.SetActive(false);

        float rand = Random.Range(8f, 10f);
        Invoke("callLightning", rand);

    }

    void callThunder1()
    {
        thunder1.SetActive(true);
        Invoke("endThunder", 3.5f);

    }
    void callThunder2()
    {
        thunder2.SetActive(true);
        LightningTWO.SetActive(true);
        Invoke("JUMP", 0.5f);
        Invoke("endThunder2", 5f);
    }
    void callThunder3()
    {
        thunder3.SetActive(true);
        LightningTHREE.SetActive(true);
        Invoke("JUMP", 2f);
       
        Invoke("endThunder3", 5f);
    }

    void endThunder()
    {
        thunder1.SetActive(false);
    }
    void endThunder2()
    {
        thunder2.SetActive(false);

    }
    void endThunder3()
    {
        thunder3.SetActive(false);
    }

    void JUMP()
    {
        LightningONE.SetActive(false);
        LightningTWO.SetActive(false);
        LightningTHREE.SetActive(false);
    }
}

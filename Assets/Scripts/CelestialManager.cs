// Unity Planetarium
// https://github.com/mchrbn/unity-planetarium-generator

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Globalization;

public struct PlanetObject{
    public GameObject gameObject;
    public PlanetName name;
}

public struct StarObject{
    public GameObject gameObject;
    public string name;
    public float ra;
    public float dec;
}

public class CelestialManager : MonoBehaviour
{
    public float latitude;                    // Your latitude
    public float longitude;                  // Your longitude
    public Vector2 planetsMinMaxRadius;     // From which distance to which distance we place the planets
    public float timeSpeedHours = 5f;     //Speed at which the planets move around Earth
    public int timeGMTOffset = 0;         //GMT offset...in this example we have the lat/long of Beijing so GMT should be set to 8
                                          //this is just for having an accurate text date
    public int numberOfDaysFromNow = 0;   //If you want to start from another than today

    public Text dateText;
    //Prefabs for celestial objects
    public GameObject moonPrefab;
    public GameObject sunPrefab;
    public GameObject marsPrefab;
    public GameObject jupiterPrefab;
    public GameObject mercuryPrefab;
    public GameObject neptunePrefab;
    public GameObject plutoPrefab;
    public GameObject saturnPrefab;
    public GameObject uranusPrefab;
    public GameObject venusPrefab;
    public GameObject starPrefab;


    private List<StarObject> stars;
    private DateTime currentDate;
    private TextAsset starsDatabase;
    private GameObject starsParent;
    private GameObject polaris;
    private bool hasLoggedMissingStarsParent;
    private bool hasLoggedMissingPolaris;

    void Start()
    {
        starsParent = GameObject.Find("Stars");
        starsDatabase = Resources.Load<TextAsset>("hyg_small") as TextAsset;
        currentDate = DateTime.UtcNow.AddDays(numberOfDaysFromNow);
        SetupStars();
    }

    void Update()
    {
        //Update time
        DateTime pastDate = currentDate;
        currentDate = currentDate.AddHours(timeSpeedHours * Time.deltaTime);
        if(dateText != null)
            dateText.text = currentDate.AddHours(timeGMTOffset).ToString();
        

        //It would be too expensive to rotate each stars individually like we did for the planets, instead rotate all the stars together
        //We just have to rotate our parent gameobject that contains all the stars with polaris as a pivot point
        //For rotation angle -> earth does a full rotation in 24hours so 15 degree per hour
        //-15 if in southern hemisphere
        if(starsParent == null){
            if(!hasLoggedMissingStarsParent){
                Debug.LogWarning("CelestialManager: Missing GameObject named 'Stars'. Create one in the scene.");
                hasLoggedMissingStarsParent = true;
            }
            return;
        }

        if(polaris == null){
            if(!hasLoggedMissingPolaris){
                Debug.LogWarning("CelestialManager: Polaris not found in loaded star data. Check hyg_small formatting/names.");
                hasLoggedMissingPolaris = true;
            }
            return;
        }

        float ellapsedH = (float)(currentDate - pastDate).TotalHours;
        if(latitude >= 0f) starsParent.transform.Rotate(polaris.transform.position, 15f * ellapsedH);
        else starsParent.transform.Rotate(polaris.transform.position, -15f * ellapsedH);
        
    }

    void SetupStars(){
        stars = new List<StarObject>();
        int loadedCount = 0;
        int skippedCount = 0;

        if(starsDatabase == null){
            Debug.LogError("CelestialManager: Could not load Resources/hyg_small as TextAsset.");
            return;
        }

        if(starPrefab == null){
            Debug.LogError("CelestialManager: starPrefab is not assigned.");
            return;
        }

        if(starsParent == null){
            Debug.LogError("CelestialManager: Missing GameObject named 'Stars'.");
            return;
        }

        string[] starsLine = starsDatabase.text.Split('\n');

        foreach(string str in starsLine){
            if(string.IsNullOrWhiteSpace(str))
            {
                skippedCount++;
                continue;
            }

            string[] data = str.Split(',');
            if(data.Length <= 13)
            {
                skippedCount++;
                continue;
            }

            //7 = ra, 8 = declination, 13 = magnitude (apparent brightness of the star), 6 = proper name
            if(!float.TryParse(data[7], NumberStyles.Float, CultureInfo.InvariantCulture, out float ra))
            {
                skippedCount++;
                continue;
            }

            if(!float.TryParse(data[8], NumberStyles.Float, CultureInfo.InvariantCulture, out float dec))
            {
                skippedCount++;
                continue;
            }

            if(!float.TryParse(data[13], NumberStyles.Float, CultureInfo.InvariantCulture, out float mag))
            {
                skippedCount++;
                continue;
            }

            InstantiateStar(ra, dec, mag, data[6]);
            loadedCount++;
        }

        Debug.Log("CelestialManager: Loaded stars=" + loadedCount + ", skipped rows=" + skippedCount);
    }

    void InstantiatePlanet(GameObject _prefab, PlanetName _name){
        PlanetObject co;
        Vector3 altAzDist = Vector3.zero;
        GameObject planetsParent = GameObject.Find("Planets");
        
        //Get the altitude (.x), azimuth (.y) and distance (.z) from Earth
        if(_name == PlanetName.MOON)
            altAzDist = CelestialCoordinates.CalculateHorizontalCoordinatesMoon(longitude, latitude, currentDate);
        else
            altAzDist = CelestialCoordinates.CalculateHorizontalCoordinatesPlanets(longitude, latitude, _name, currentDate);
        
        //Instantiate the corresponding prefab + convert alt/az/dist to game scene
        co.gameObject = Instantiate(_prefab, GetPlanetsGamePositionFromAltAz(altAzDist), Quaternion.identity);
        co.gameObject.name = _name.ToString();
        co.name = _name;
        co.gameObject.transform.SetParent(planetsParent.transform);
    }

    void InstantiateStar(float _ra, float _dec, float _mag, string _name){
        StarObject so;
        
        //Get the altitude and azimuth of the star
        Vector2 altAz =  CelestialCoordinates.CalculateHorizontalCoordinatesStar(longitude, latitude, _ra, _dec, currentDate);

        //Instantiate the gameobject
        Vector3 pos = Quaternion.Euler(-altAz.x, altAz.y, 0) * new Vector3(0, 0, 1000);

        //Set properties to the struct
        so.gameObject = Instantiate(starPrefab, pos, Quaternion.identity);
        so.name = _name;
        so.ra = _ra;
        so.dec = _dec;
        if(_name != "") so.gameObject.name = _name;
        so.gameObject.transform.SetParent(starsParent.transform);

        //Change the luminosity of the material according to the star's magnitude
        //The lower a magnitude is, the most intense the luminosity is
        Material mat = so.gameObject.GetComponent<Renderer>().material;
        mat.SetColor("_EmissionColor", Color.white * Mathf.Max(7 - _mag, 1));

        stars.Add(so);

        //Save polaris (or polaris octantis for souther hemisphere) for later - we need to rotate our universe around it
        if((_name == "Polaris" && latitude >= 0f) || (_name == "Polaris Octantis" && latitude < 0f))
            polaris = so.gameObject;
    }

    Vector3 GetPlanetsGamePositionFromAltAz(Vector3 _altAz){
        //Remap AU to our scene distance
        float distance = Map(_altAz.z, 0, 40, 2f, 50f);
        Vector3 altAzDist = Quaternion.Euler(-_altAz.x, _altAz.y, 0) * new Vector3(0, 0, distance);
        return altAzDist;
    }

    float Map(float s, float a1, float a2, float b1, float b2){
		if (a1 == a2)
			return b1;
        
		return b1 + (s-a1)*(b2-b1)/(a2-a1);
	}
}

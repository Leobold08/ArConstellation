using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Globalization;
using TMPro;

public struct PlanetObject
{
    public GameObject gameObject;
    public PlanetName name;
}

public struct StarObject
{
    public GameObject gameObject;
    public string id;
    public string name;
    public float ra;
    public float dec;
}

public class CelestialManager : MonoBehaviour
{
    public float latitude;                    // Your latitude
    public float longitude;                   // Your longitude
    public Vector2 planetsMinMaxRadius;       // From which distance to which distance we place the planets
    public float timeSpeedHours = 5f;         // Speed at which the planets move around Earth
    public int timeGMTOffset = 0;             // GMT offset
                                             // This is just for having an accurate text date
    public int numberOfDaysFromNow = 0;       // If you want to start from another than today

    [Header("Star Size")]

    // HYG apparent magnitude range being used.
    // Lower magnitude = brighter star.
    private float brightestMagnitude = -1.44f;
    private float dimmestMagnitude = 6.59f;

    // Star scale range.
    // This is kept completely separate from star depth.
    public float brightestScale = 3f;
    public float dimmestScale = 0.5f;


    [Header("Star Depth")]

    // Normal celestial sphere distance.
    public float baseStarDistance = 1000f;

    // Closest stars can be this much closer than the normal sphere.
    // 1000 - 300 = 700
    public float closestStarOffset = -300f;

    // Furthest stars can be this much further than the normal sphere.
    // 1000 + 100 = 1100
    public float furthestStarOffset = 100f;

    // Distance mapping breakpoints in parsecs.
    public float nearStarDistance = 200f;
    public float middleStarDistance = 1000f;
    public float farStarDistance = 10000f;


    public TMP_Text dateText;

    // Prefabs for celestial objects
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

        starsDatabase =
            Resources.Load<TextAsset>("hygdata_short") as TextAsset;

        currentDate =
            DateTime.UtcNow.AddDays(numberOfDaysFromNow);

        SetupStars();
    }


    private bool StarsIntersect(GameObject a, GameObject b)
    {
        Renderer rendererA = a.GetComponent<Renderer>();
        Renderer rendererB = b.GetComponent<Renderer>();

        if (rendererA == null || rendererB == null)
            return false;

        return rendererA.bounds.Intersects(rendererB.bounds);
    }


    [ContextMenu("Generate Star Collision Report")]
    public void GenerateStarCollisionReport()
    {
        if (stars == null || stars.Count == 0)
        {
            Debug.LogWarning(
                "CelestialManager: No stars have been loaded."
            );

            return;
        }

        // A collision cluster is a group of stars where every star is
        // connected to another star through one or more intersections.
        List<List<int>> collisionClusters =
            new List<List<int>>();

        bool[] visited = new bool[stars.Count];


        for (int i = 0; i < stars.Count; i++)
        {
            if (visited[i])
                continue;

            List<int> cluster =
                new List<int>();

            Queue<int> queue =
                new Queue<int>();

            queue.Enqueue(i);
            visited[i] = true;


            while (queue.Count > 0)
            {
                int current = queue.Dequeue();

                bool currentHasCollision = false;


                for (int j = 0; j < stars.Count; j++)
                {
                    if (current == j || visited[j])
                        continue;


                    if (StarsIntersect(
                        stars[current].gameObject,
                        stars[j].gameObject))
                    {
                        currentHasCollision = true;

                        visited[j] = true;
                        queue.Enqueue(j);
                    }
                }


                if (currentHasCollision || cluster.Count > 0)
                    cluster.Add(current);
            }


            // A single isolated star isn't a collision.
            if (cluster.Count > 1)
                collisionClusters.Add(cluster);
        }


        string path =
            System.IO.Path.Combine(
                Application.dataPath,
                "Scripts",
                "star_collision_report.txt"
            );


        using (
            System.IO.StreamWriter writer =
            new System.IO.StreamWriter(path, false)
        )
        {
            writer.WriteLine(
                "STAR COLLISION REPORT"
            );

            writer.WriteLine(
                "====================="
            );

            writer.WriteLine();

            writer.WriteLine(
                "Generated: " + DateTime.Now
            );

            writer.WriteLine(
                "Total stars: " + stars.Count
            );

            writer.WriteLine(
                "Collision groups: " +
                collisionClusters.Count
            );

            writer.WriteLine();


            foreach (List<int> cluster in collisionClusters)
            {
                int keepIndex = cluster[0];


                // Find the largest star in this collision group.
                foreach (int index in cluster)
                {
                    float currentScale =
                        stars[index]
                        .gameObject
                        .transform
                        .localScale
                        .x;


                    float keepScale =
                        stars[keepIndex]
                        .gameObject
                        .transform
                        .localScale
                        .x;


                    if (currentScale > keepScale)
                        keepIndex = index;
                }


                writer.WriteLine(
                    "COLLISION GROUP"
                );

                writer.WriteLine(
                    "----------------"
                );


                writer.WriteLine(
                    "KEEP: ID=" +
                    stars[keepIndex].id +
                    " Name=" +
                    stars[keepIndex].name +
                    " Scale=" +
                    stars[keepIndex]
                    .gameObject
                    .transform
                    .localScale
                    .x
                    .ToString("F3")
                );


                foreach (int index in cluster)
                {
                    if (index == keepIndex)
                        continue;


                    writer.WriteLine(
                        "DELETE: ID=" +
                        stars[index].id +
                        " Name=" +
                        stars[index].name +
                        " Scale=" +
                        stars[index]
                        .gameObject
                        .transform
                        .localScale
                        .x
                        .ToString("F3")
                    );
                }


                writer.WriteLine();
            }
        }


        Debug.Log(
            "CelestialManager: Star collision report generated at:\n" +
            path
        );
    }


    void Update()
    {
        // Update time
        DateTime pastDate = currentDate;

        currentDate =
            currentDate.AddHours(
                timeSpeedHours *
                Time.deltaTime
            );


        if (dateText != null)
        {
            dateText.text =
                currentDate
                .AddHours(timeGMTOffset)
                .ToString();
        }


        // It would be too expensive to rotate each star individually.
        // Instead rotate all the stars together.
        //
        // We rotate the parent GameObject containing all stars
        // with Polaris as a pivot point.
        //
        // Earth rotates 15 degrees per hour.
        // -15 if in southern hemisphere.

        if (starsParent == null)
        {
            if (!hasLoggedMissingStarsParent)
            {
                Debug.LogWarning(
                    "CelestialManager: Missing GameObject named 'Stars'. " +
                    "Create one in the scene."
                );

                hasLoggedMissingStarsParent = true;
            }

            return;
        }


        if (polaris == null)
        {
            if (!hasLoggedMissingPolaris)
            {
                Debug.LogWarning(
                    "CelestialManager: Polaris not found in loaded star data. " +
                    "Check hyg_small formatting/names."
                );

                hasLoggedMissingPolaris = true;
            }

            return;
        }


        float ellapsedH =
            (float)(
                currentDate - pastDate
            ).TotalHours;


        if (latitude >= 0f)
        {
            starsParent.transform.Rotate(
                polaris.transform.position,
                15f * ellapsedH
            );
        }
        else
        {
            starsParent.transform.Rotate(
                polaris.transform.position,
                -15f * ellapsedH
            );
        }
    }


    void SetupStars()
    {
        stars =
            new List<StarObject>();

        int loadedCount = 0;
        int skippedCount = 0;


        if (starsDatabase == null)
        {
            Debug.LogError(
                "CelestialManager: Could not load " +
                "Resources/hygdata_short as TextAsset."
            );

            return;
        }


        if (starPrefab == null)
        {
            Debug.LogError(
                "CelestialManager: starPrefab is not assigned."
            );

            return;
        }


        if (starsParent == null)
        {
            Debug.LogError(
                "CelestialManager: Missing GameObject named 'Stars'."
            );

            return;
        }


        string[] starsLine =
            starsDatabase.text.Split('\n');


        foreach (string str in starsLine)
        {
            if (string.IsNullOrWhiteSpace(str))
            {
                skippedCount++;
                continue;
            }


            string[] data =
                str.Split(',');


            if (data.Length <= 13)
            {
                skippedCount++;
                continue;
            }


            // HYG columns:
            //
            // 0  = id
            // 6  = proper name
            // 7  = ra
            // 8  = declination
            // 9  = distance (parsecs)
            // 13 = magnitude


            if (!float.TryParse(
                data[7],
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out float ra
            ))
            {
                skippedCount++;
                continue;
            }


            if (!float.TryParse(
                data[8],
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out float dec
            ))
            {
                skippedCount++;
                continue;
            }


            if (!float.TryParse(
                data[9],
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out float dist
            ))
            {
                skippedCount++;
                continue;
            }


            if (!float.TryParse(
                data[13],
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out float mag
            ))
            {
                skippedCount++;
                continue;
            }


            InstantiateStar(
                data[0],
                ra,
                dec,
                dist,
                mag,
                data[6]
            );


            loadedCount++;
        }


        Debug.Log(
            "CelestialManager: Loaded stars=" +
            loadedCount +
            ", skipped rows=" +
            skippedCount
        );
    }


    void InstantiatePlanet(
        GameObject _prefab,
        PlanetName _name)
    {
        PlanetObject co;

        Vector3 altAzDist =
            Vector3.zero;

        GameObject planetsParent =
            GameObject.Find("Planets");


        // Get the altitude (.x), azimuth (.y)
        // and distance (.z) from Earth.

        if (_name == PlanetName.MOON)
        {
            altAzDist =
                CelestialCoordinates
                .CalculateHorizontalCoordinatesMoon(
                    longitude,
                    latitude,
                    currentDate
                );
        }
        else
        {
            altAzDist =
                CelestialCoordinates
                .CalculateHorizontalCoordinatesPlanets(
                    longitude,
                    latitude,
                    _name,
                    currentDate
                );
        }


        // Instantiate the corresponding prefab
        // and convert alt/az/dist to game scene.

        co.gameObject =
            Instantiate(
                _prefab,
                GetPlanetsGamePositionFromAltAz(
                    altAzDist
                ),
                Quaternion.identity
            );


        co.gameObject.name =
            _name.ToString();

        co.name = _name;

        co.gameObject.transform.SetParent(
            planetsParent.transform
        );
    }


    void InstantiateStar(
        string _id,
        float _ra,
        float _dec,
        float _dist,
        float _mag,
        string _name)
    {
        StarObject so;


        // Get the altitude and azimuth of the star.
        Vector2 altAz =
            CelestialCoordinates
            .CalculateHorizontalCoordinatesStar(
                longitude,
                latitude,
                _ra,
                _dec,
                currentDate
            );


        // ---------------------------------------------------------
        // STAR DEPTH
        //
        // 0-200 pc       -> 700-1000
        // 200-1000 pc    -> 1000-1050
        // 1000-10000 pc  -> 1050-1100
        // 10000+ pc      -> 1100
        // ---------------------------------------------------------

        float starDistance;


        if (_dist <= nearStarDistance)
        {
            // 0 -> 200 pc
            // 700 -> 1000

            float t =
                Mathf.InverseLerp(
                    0f,
                    nearStarDistance,
                    _dist
                );


            starDistance =
                Mathf.Lerp(
                    baseStarDistance + closestStarOffset,
                    baseStarDistance,
                    t
                );
        }
        else if (_dist <= middleStarDistance)
        {
            // 200 -> 1000 pc
            // 1000 -> 1050

            float t =
                Mathf.InverseLerp(
                    nearStarDistance,
                    middleStarDistance,
                    _dist
                );


            starDistance =
                Mathf.Lerp(
                    baseStarDistance,
                    baseStarDistance + 50f,
                    t
                );
        }
        else if (_dist <= farStarDistance)
        {
            // 1000 -> 10000 pc
            // 1050 -> 1100

            float t =
                Mathf.InverseLerp(
                    middleStarDistance,
                    farStarDistance,
                    _dist
                );


            starDistance =
                Mathf.Lerp(
                    baseStarDistance + 50f,
                    baseStarDistance + furthestStarOffset,
                    t
                );
        }
        else
        {
            // 10000+ pc
            // Clamp everything to 1100.

            starDistance =
                baseStarDistance +
                furthestStarOffset;
        }


        Vector3 pos =
            Quaternion.Euler(
                -altAz.x,
                altAz.y,
                0
            ) *
            new Vector3(
                0,
                0,
                starDistance
            );


        // Instantiate star.
        so.gameObject =
            Instantiate(
                starPrefab,
                pos,
                Quaternion.identity
            );


        // ---------------------------------------------------------
        // STAR SCALE
        //
        // Scale is determined ONLY by magnitude.
        // Distance has NO effect on scale.
        // ---------------------------------------------------------

        float scale =
            Mathf.Lerp(
                brightestScale,
                dimmestScale,
                Mathf.InverseLerp(
                    brightestMagnitude,
                    dimmestMagnitude,
                    _mag
                )
            );


        so.gameObject.transform.localScale =
            Vector3.one * scale;


        // Store star properties.

        so.name = _name;
        so.ra = _ra;
        so.dec = _dec;
        so.id = _id;


        // Name unnamed stars using their HYG ID.

        if (!string.IsNullOrWhiteSpace(_name))
        {
            so.gameObject.name =
                _name;
        }
        else
        {
            so.gameObject.name =
                "Star_" + _id;
        }


        so.gameObject.transform.SetParent(
            starsParent.transform
        );


        // Change luminosity of the material
        // according to the star's magnitude.
        //
        // Lower magnitude = brighter.

        Renderer renderer =
            so.gameObject.GetComponent<Renderer>();


        if (renderer != null)
        {
            Material mat =
                renderer.material;


            mat.SetColor(
                "_EmissionColor",
                Color.white *
                Mathf.Max(
                    7 - _mag,
                    1
                )
            );
        }


        stars.Add(so);


        // Save Polaris (or Polaris Octantis for
        // southern hemisphere) for later.
        //
        // We use it as the pivot for rotating
        // the celestial sphere.

        if (
            (_name == "Polaris" && latitude >= 0f) ||
            (_name == "Polaris Octantis" && latitude < 0f)
        )
        {
            polaris =
                so.gameObject;
        }
    }


    Vector3 GetPlanetsGamePositionFromAltAz(
        Vector3 _altAz)
    {
        // Remap AU to our scene distance.

        float distance =
            Map(
                _altAz.z,
                0,
                40,
                2f,
                50f
            );


        Vector3 altAzDist =
            Quaternion.Euler(
                -_altAz.x,
                _altAz.y,
                0
            ) *
            new Vector3(
                0,
                0,
                distance
            );


        return altAzDist;
    }


    float Map(
        float s,
        float a1,
        float a2,
        float b1,
        float b2)
    {
        if (a1 == a2)
            return b1;


        return b1 +
               (s - a1) *
               (b2 - b1) /
               (a2 - a1);
    }
}
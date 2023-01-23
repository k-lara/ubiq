using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerRaycasting : MonoBehaviour
{
    // Write black pixels onto the GameObject that is located
// by the script. The script is attached to the camera.
// Determine where the collider hits and modify the texture at that point.
//
// Note that the MeshCollider on the GameObject must have Convex turned off. This allows
// concave GameObjects to be included in collision in this example.
//
// Also to allow the texture to be updated by mouse button clicks it must have the Read/Write
// Enabled option set to true in its Advanced import settings.

    public Camera cam;
    private bool raycastingEnabled = false;
    private List<UVCoordinates> UVs;
    private int Frames = 0;
    private Queue<TexturePosition> texPosQueue1;
    private Queue<TexturePosition> texPosQueue2;

    // indicates whether an avatar (distinguishable by texture) is on the left or right
    // in round 1 this is the same for recording 1 and recording 2 so this is only half the size
    private TexturePosition[] texturePositionsFirstRound = new TexturePosition[]
    {
        new TexturePosition("athleteFemaleYellow", "L"), //  1
        new TexturePosition("skaterFemaleA", "L"), // 2
        new TexturePosition("skaterFemaleA", "L"), // 3 
        new TexturePosition("athleteFemaleYellow", "L"), // 4
        new TexturePosition("casualFemaleB", "L"), // 5
        new TexturePosition("skaterFemaleA", "L"), // 6

        new TexturePosition("skaterFemaleA", "L"), // 2
        new TexturePosition("skaterFemaleA", "L"), // 3 
         new TexturePosition("athleteFemaleYellow", "L"), // 4
        new TexturePosition("casualFemaleB", "L"), // 5
        new TexturePosition("skaterFemaleA", "L"), // 6
        new TexturePosition("athleteFemaleYellow", "L"), // 1
    };

    private TexturePosition[] texturePositionsSecondRound = new TexturePosition[]
{
        new TexturePosition("athleteFemaleYellow", "L"), //  1
        new TexturePosition("skaterFemaleA", "L"), // 2
        new TexturePosition("skaterFemaleA", "L"), // 3 
        new TexturePosition("athleteFemaleYellow", "L"), // 4
        new TexturePosition("casualFemaleB", "L"), // 5
        new TexturePosition("skaterFemaleA", "L"), // 2
        new TexturePosition("skaterFemaleA", "L"), // 6
        new TexturePosition("skaterFemaleA", "L"), // 3 
        new TexturePosition("casualFemaleB", "L"), // 5
        new TexturePosition("athleteFemaleYellow", "L"), // 4
        new TexturePosition("skaterFemaleA", "L"), // 6
        new TexturePosition("skaterFemaleA", "L"), // 2
        new TexturePosition("casualFemaleB", "L"), // 5
        new TexturePosition("athleteFemaleYellow", "L"), //  1
        new TexturePosition("skaterFemaleA", "L"), // 3 
        new TexturePosition("athleteFemaleYellow", "L"), // 4
        new TexturePosition("skaterFemaleA", "L"), // 2
        new TexturePosition("skaterFemaleA", "L"), // 6
        new TexturePosition("athleteFemaleYellow", "L"), //  1
        new TexturePosition("athleteFemaleYellow", "L"), // 4
        new TexturePosition("skaterFemaleA", "L"), // 3 
        new TexturePosition("athleteFemaleYellow", "L"), // 1
        new TexturePosition("skaterFemaleA", "L"), // 6
        new TexturePosition("casualFemaleB", "L"), // 5
};

    [System.Serializable]
    public class TexturePosition
    {
        // because there are only 2 actors the position of the second texture is whatever the first texture is not.
        public string textureName; // name of texture
        public string position; // L (left) or R (right)
        public TexturePosition(string textureName, string position)
        {
            this.textureName = textureName;
            this.position = position;
        }
    }

    public TexturePosition Dequeue(int round)
    {
        if (round == 1)
        {
            return texPosQueue1.Dequeue();
        }
        else
        {
            return texPosQueue2.Dequeue();
        }
    }

    void Start()
    {
        UVs = new List<UVCoordinates>();
        texPosQueue1 = new Queue<TexturePosition>(texturePositionsFirstRound);
        texPosQueue2 = new Queue<TexturePosition>(texturePositionsSecondRound);
    }

    public RaycastInfo GetRaycastInfo()
    {
        return new RaycastInfo(new List<UVCoordinates>(UVs), Frames);
    }

    [System.Serializable]
    public class RaycastInfo
    {
        // save uv coordinates of where people look at on the avatars
        public int frames;
        public TexturePosition texPos;
        public List<UVCoordinates> UVs;

        public RaycastInfo(List<UVCoordinates> UVs, int frames)
        {
            this.UVs = UVs; 
            this.frames = frames;
        }
    }

    [System.Serializable]
    public class UVCoordinates
    {
        public float u;
        public float v;
        public string texture;

        public UVCoordinates(float u, float v, string texture)
        {
            this.u = u;
            this.v = v;
            this.texture = texture;
        }
    }

    void Update()
    {

        if (raycastingEnabled)
        {
            Frames++;
            RaycastHit hit;
            if (!Physics.Raycast(cam.transform.position, cam.transform.forward, out hit, 10.0f))
                return;
            //Debug.Log("here");

            Renderer rend = hit.transform.GetComponent<Renderer>();
            MeshCollider meshCollider = hit.collider as MeshCollider;
            //Debug.Log(hit.collider.ToString());
            if (rend == null || rend.sharedMaterial == null || rend.sharedMaterial.mainTexture == null || meshCollider == null)
                return;

            Texture2D tex = rend.material.mainTexture as Texture2D;
            Vector2 pixelUV = hit.textureCoord;
            //Debug.Log(pixelUV.x + " " + pixelUV.y);
            pixelUV.x *= tex.width;
            pixelUV.y *= tex.height;
            UVs.Add(new UVCoordinates(pixelUV.x, pixelUV.y, tex.name));
            //Debug.Log(pixelUV.x + " " + pixelUV.y + " " + tex.name);
            //tex.SetPixel((int)pixelUV.x, (int)pixelUV.y, Color.black);
            //tex.Apply();
        }
        else
        {
            if (UVs.Count > 0)
            {
                //Debug.Log("Clear UV list");
                Frames = 0;
                UVs.Clear();
            }
        }
    }

    public bool RaycastingEnabled(bool enabled)
    {
        raycastingEnabled = enabled;
        return enabled;
    }
}


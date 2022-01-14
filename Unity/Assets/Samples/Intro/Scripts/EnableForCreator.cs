using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Ubiq.Messaging;
using Ubiq.Rooms;

// only the creator of a room (first person that joined) should record and replay
public class EnableForCreator : MonoBehaviour
{
    public NetworkScene scene;
    public Button recordReplayButtonMain;
    private RoomClient roomClient;
    


    // Start is called before the first frame update
    void Start()
    {
        roomClient = scene.GetComponent<RoomClient>();
        roomClient.OnPeerUpdated.AddListener(OnPeerUpdated);
    }

    public void OnPeerUpdated(IPeer peer)
    {
        if (peer.UUID == roomClient.Me.UUID) // check this otherwise we also update wrong peer and hide menu accidentally
        {
            UpdateMenu(peer);
        }
    }
    private void UpdateMenu(IPeer peer)
    {
        if (peer["creator"] == "1")
        {
            Debug.Log("Menu: creator");
            recordReplayButtonMain.interactable = true;            
        }
        else
        {
            Debug.Log("Menu: NOT creator");
            recordReplayButtonMain.interactable = false;
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}

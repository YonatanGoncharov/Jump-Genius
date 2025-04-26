using UnityEngine;

public class MenuUI : MonoBehaviour
{
    [SerializeField] private GameManager gm;   // drag GameManager in Inspector

    public void OnClickTrain()
    {
        gameObject.SetActive(false);           // hide the whole menu
        gm.UI_Train();                         // start training
    }

    public void OnClickReplayBest()
    {
        gameObject.SetActive(false);           // hide the whole menu
        gm.UI_Replay();                        // spawn champion AI
    }
}

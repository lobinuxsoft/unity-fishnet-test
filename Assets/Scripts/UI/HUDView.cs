using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using FishNet;
using FishNet.Discovery;
using FishNet.Transporting;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HUDView : MonoBehaviour
{
    private const string SERVER = "Server";
    private const string LAN = "LAN Functionality";
    private const string SEARCH = "Search Servers";
    private const string START = "Start";
    private const string STOP = "Stop";
    public static HUDView Instance { get; private set; }

    [SerializeField] private NetworkDiscovery networkDiscovery;
    [SerializeField] private Button serverButton;
    [SerializeField] private TextMeshProUGUI serverButtonText;
    [SerializeField] private Button lanButton;
    [SerializeField] private TextMeshProUGUI lanButtonText;
    [SerializeField] private Button searchButton;
    [SerializeField] private TextMeshProUGUI searchButtonText;
    [SerializeField] private Button clientButton;

    [SerializeField] private CanvasGroup searchResultCanvasGroup;
    [SerializeField] private Transform container;
    [SerializeField] private Button ipButtonPrefab;

    private List<IPEndPoint> endPoints = new List<IPEndPoint>();
    private CanvasGroup canvasGroup;

    private void Start()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        canvasGroup = GetComponent<CanvasGroup>();

        serverButton.onClick.AddListener(OnServerButtonClicked);
        lanButton.onClick.AddListener(OnLanButtonClicked);
        searchButton.onClick.AddListener(OnSearchButtonClicked);
        clientButton.onClick.AddListener(OnClientButtonClicked);
        clientButton.gameObject.SetActive(false);

        networkDiscovery.ServerFoundCallback += OnServerFoundCallback;
        InstanceFinder.ServerManager.OnServerConnectionState += OnServerConnectionState;
        InstanceFinder.ClientManager.OnClientConnectionState += OnClientConnectionState;
    }

    private void LateUpdate()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            if (canvasGroup.interactable)
                Hide();
            else
                Show();

        if (canvasGroup.interactable && Time.frameCount % 5 == 0)
        {
            serverButtonText.text = $"{(InstanceFinder.IsServerStarted ? STOP : START)} {SERVER}";
            lanButtonText.text = $"{(networkDiscovery.IsAdvertising ? STOP : START)} {LAN}";
            searchButtonText.text = $"{(networkDiscovery.IsSearching ? STOP : START)} {SEARCH}";
        }
    }

    private void OnDestroy()
    {
        serverButton.onClick.RemoveListener(OnServerButtonClicked);
        lanButton.onClick.RemoveListener(OnLanButtonClicked);
        searchButton.onClick.RemoveListener(OnSearchButtonClicked);
        clientButton.onClick.RemoveListener(OnClientButtonClicked);

        networkDiscovery.ServerFoundCallback -= OnServerFoundCallback;
        InstanceFinder.ServerManager.OnServerConnectionState -= OnServerConnectionState;
        InstanceFinder.ClientManager.OnClientConnectionState -= OnClientConnectionState;
    }

    private void OnServerFoundCallback(IPEndPoint endPoint)
    {
        if (!endPoints.Contains(endPoint))
        {
            endPoints.Add(endPoint);

            string ipAddress = endPoint.Address.ToString();
            Button button = Instantiate(ipButtonPrefab, container);

            TextMeshProUGUI textMP = button.GetComponentInChildren<TextMeshProUGUI>();
            textMP.text = $"{ipAddress}:{endPoint.Port}";

            button.onClick.AddListener(() =>
            {
                networkDiscovery.StopSearching();
                InstanceFinder.ClientManager.StartConnection(ipAddress);
            });

            ShowSearchResult();
        }
    }

    private void OnServerButtonClicked()
    {
        if (InstanceFinder.IsServerStarted)
            InstanceFinder.ServerManager.StopConnection(true);
        else
            InstanceFinder.ServerManager.StartConnection();
    }

    private void OnLanButtonClicked()
    {
        if (networkDiscovery.IsAdvertising)
            networkDiscovery.StopAdvertising();
        else
            networkDiscovery.AdvertiseServer();
    }

    private void OnSearchButtonClicked()
    {
        if (networkDiscovery.IsSearching)
        {
            networkDiscovery.StopSearching();
        }
        else
        {
            StopAllCoroutines();
            StartCoroutine(SearchRoutine());
        }
    }

    IEnumerator SearchRoutine()
    {
        HideSearchResult();

        endPoints.Clear();

        foreach (Button button in container.GetComponentsInChildren<Button>())
        {
            button.onClick.RemoveAllListeners();
            Destroy(button.gameObject);
        }

        yield return new WaitForEndOfFrame();

        networkDiscovery.SearchForServers();
    }

    private void OnServerConnectionState(ServerConnectionStateArgs server)
    {
        switch (server.ConnectionState)
        {
            case LocalConnectionState.Stopped:
                networkDiscovery.StopSearchingOrAdvertising();
                HideSearchResult();
                break;
        }
    }
    
    private void OnClientConnectionState(ClientConnectionStateArgs client)
    {
        switch (client.ConnectionState)
        {
            case LocalConnectionState.Stopped:
                clientButton.gameObject.SetActive(false);
                Show();
                break;
            case LocalConnectionState.Started:
                clientButton.gameObject.SetActive(true);
                HideSearchResult();
                Hide();
                break;
        }
    }

    private void OnClientButtonClicked() => InstanceFinder.ClientManager.StopConnection();

    private void ShowSearchResult()
    {
        searchResultCanvasGroup.alpha = 1;
        searchResultCanvasGroup.interactable = true;
        searchResultCanvasGroup.blocksRaycasts = true;
    }

    private void HideSearchResult()
    {
        searchResultCanvasGroup.alpha = 0;
        searchResultCanvasGroup.interactable = false;
        searchResultCanvasGroup.blocksRaycasts = false;
    }

    public void Show()
    {
        canvasGroup.alpha = 1;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void Hide()
    {
        canvasGroup.alpha = 0;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}
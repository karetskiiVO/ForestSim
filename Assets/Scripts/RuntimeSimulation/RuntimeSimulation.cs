using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;

using Cysharp.Threading.Tasks;

using ProceduralVegetation;
using ProceduralVegetation.Utilities;

using Sirenix.OdinInspector;

using UnityEngine;

class RuntimeSimulation : MonoBehaviour {
    [Serializable]
    private class TunerStartRequestPayload {
        public string action;
        public int trials;
        public int horizonYears;
        public SpeciesBalancePayload[] species;
    }

    [Serializable]
    private class TunerStartResponsePayload {
        public string sessionId;
        public string error;
    }

    [Serializable]
    private class TunerAskRequestPayload {
        public string action;
        public string sessionId;
    }

    [Serializable]
    private class TunerAskResponsePayload {
        public bool done;
        public int trialId;
        public SpeciesTuning[] tunings;
        public string error;
    }

    [Serializable]
    private class TunerTellRequestPayload {
        public string action;
        public string sessionId;
        public int trialId;
        public float objective;
    }

    [Serializable]
    private class TunerTellResponsePayload {
        public bool done;
        public string error;
    }

    [Serializable]
    private class TunerBestRequestPayload {
        public string action;
        public string sessionId;
    }

    [Serializable]
    private class TunerCloseRequestPayload {
        public string action;
        public string sessionId;
    }

    private class SpeciesSetup {
        public string speciesName;
        public TreeSpeciesDescriptor descriptor;
        public Vector2[] initialPoints;
    }

    [Serializable]
    private struct SpeciesTargetShare {
        public string speciesName;
        [Range(0f, 1f)]
        public float targetShare;
    }

    [Serializable]
    private class SpeciesBalancePayload {
        public string speciesName;
        public float currentShare;
        public float targetShare;
        public float growthMultiplier;
        public float mortalityMultiplier;
        public float seedingMultiplier;
    }

    [Serializable]
    private class BalanceRequestPayload {
        public int trials;
        public int horizonYears;
        public SpeciesBalancePayload[] species;
    }

    [Serializable]
    private class BalanceResponsePayload {
        public SpeciesTuning[] tunings;
        public float objective;
    }

    Simulation simulation;
    BakedLandscape bakedLandscape;

    [SerializeField]
    RuntimeSpeciesContainer[] speciesContainers;

    [Header("Python Balancer")]
    [SerializeField]
    private string tunerHost = "127.0.0.1";

    [SerializeField]
    private int tunerPort = 5057;

    [SerializeField, Min(10)]
    private int balancingTrials = 120;

    [SerializeField, Min(1)]
    private int balancingHorizonYears = 30;

    [SerializeField]
    private SpeciesTargetShare[] targetDistribution;

    [Header("Objective Weights")]
    [SerializeField, Min(0f)]
    private float speciesDistributionWeight = 1f;

    [SerializeField, Min(0f)]
    private float treeCountWeight = 0.9f;

    [SerializeField, Min(0.1f)]
    private float targetTreeCountMultiplier = 1.3f;

    [SerializeField, Min(1)]
    private int minimumTargetTreeCount = 140;

    [SerializeField]
    private bool autoStartPythonServer = true;

    [SerializeField]
    private string pythonExecutable = "python";

    [SerializeField]
    private string pythonScriptRelativePath = "tools/species_tuner_server.py";

    [SerializeField, Min(1000)]
    private int pythonServerStartupTimeoutMs = 8000;

    private System.Diagnostics.Process pythonServerProcess;
    private bool pythonServerStartedByRuntime;
    private bool isBalancing;
    private List<SpeciesSetup> speciesSetups;
    private bool simulationInitialized;

    private void Start() {
        EnsureSimulationInitialized();

        if (simulation == null) {
            Debug.LogError("Failed to initialize simulation in Start().");
            return;
        }

        _ = Run();
    }

    private void EnsureSimulationInitialized() {
        if (simulationInitialized) {
            return;
        }

        var landscape = new RidgedNoiseLandscapeDescriptor() {
            bbox = new Bounds(new Vector3(0, 0, 0), new Vector3(1000, 21, 1000)),
            offset = new Vector2(0, 0),
            octaves = 4,
            lacunarity = 2f,
            persistence = 0.74f,
            sharpness = 0.5f
        };
        bakedLandscape = landscape.Bake(new() { resolution = new(512, 512) });
        DrawLandscape();

        simulation = new Simulation()
            .SetLandscape(bakedLandscape);

        speciesSetups = new List<SpeciesSetup>(speciesContainers != null ? speciesContainers.Length : 0);
        if (speciesContainers != null) {
            foreach (var speciesContainer in speciesContainers) {
                if (speciesContainer == null) {
                    continue;
                }

                var descriptor = speciesContainer.GetDescriptor();
                var initialPoints = speciesContainer.GetInitialPoints(bakedLandscape.bbox);

                speciesSetups.Add(new SpeciesSetup {
                    speciesName = descriptor.GetType().Name,
                    descriptor = descriptor,
                    initialPoints = initialPoints,
                });

                simulation.AddSpecies(descriptor, initialPoints);
            }
        }

        simulationInitialized = true;
    }

    private async UniTaskVoid Run() {
        if (simulation == null) {
            Debug.LogError("Run() called before simulation initialization.");
            return;
        }

        simulation.AddEventGenerator(new BaseEventGenerator());

        while (true) {
            simulation.Run(1);
            DrawTrees();
            await UniTask.Delay(TimeSpan.FromSeconds(0.1));
        }
    }

    [Button]
    public void BalanceViaPythonMenu() {
        _ = BalanceWithPython();
    }

    public async UniTaskVoid BalanceWithPython() {
        EnsureSimulationInitialized();

        if (simulation == null) {
            Debug.LogWarning("Simulation is not initialized yet.");
            return;
        }

        if (!await EnsurePythonServerRunning()) {
            Debug.LogError("Python balancer is not reachable.");
            return;
        }

        var request = BuildBalanceRequest();
        if (request.species == null || request.species.Length == 0) {
            Debug.LogWarning("No species data available for balancing.");
            return;
        }

        isBalancing = true;
        try {
            var startResponse = await StartTunerSession(request);
            if (startResponse == null) {
                return;
            }

            for (int i = 0; i < balancingTrials; i++) {
                var ask = await AskTrial(startResponse.sessionId);
                if (ask == null || !string.IsNullOrEmpty(ask.error)) {
                    Debug.LogError($"Ask trial failed: {ask?.error}");
                    break;
                }

                if (ask.done) {
                    break;
                }

                float objective = EvaluateObjectiveWithSimulation(ask.tunings, balancingHorizonYears);
                var tell = await TellTrial(startResponse.sessionId, ask.trialId, objective);
                if (tell == null || !string.IsNullOrEmpty(tell.error)) {
                    Debug.LogError($"Tell trial failed: {tell?.error}");
                    break;
                }
            }

            var best = await GetBestResult(startResponse.sessionId);
            if (best?.tunings == null || best.tunings.Length == 0) {
                Debug.LogWarning("Python balancer returned no tuning values.");
                await CloseSession(startResponse.sessionId);
                return;
            }

            SpeciesTuningRegistry.Apply(best.tunings);
            Debug.Log($"Applied {best.tunings.Length} tuning values. Objective={best.objective:0.0000}");

            await CloseSession(startResponse.sessionId);
        } catch (Exception ex) {
            Debug.LogError($"Balance request failed: {ex.Message}");
            return;
        } finally {
            isBalancing = false;
        }
    }

    private async UniTask<TunerStartResponsePayload> StartTunerSession(BalanceRequestPayload baseRequest) {
        var startRequest = new TunerStartRequestPayload {
            action = "start",
            trials = baseRequest.trials,
            horizonYears = baseRequest.horizonYears,
            species = baseRequest.species,
        };

        var response = await SendAndParse<TunerStartResponsePayload>(startRequest);
        if (response == null || !string.IsNullOrEmpty(response.error) || string.IsNullOrEmpty(response.sessionId)) {
            Debug.LogError($"Failed to start tuning session: {response?.error}");
            return null;
        }

        return response;
    }

    private async UniTask<TunerAskResponsePayload> AskTrial(string sessionId) {
        var request = new TunerAskRequestPayload {
            action = "ask",
            sessionId = sessionId,
        };
        return await SendAndParse<TunerAskResponsePayload>(request);
    }

    private async UniTask<TunerTellResponsePayload> TellTrial(string sessionId, int trialId, float objective) {
        var request = new TunerTellRequestPayload {
            action = "tell",
            sessionId = sessionId,
            trialId = trialId,
            objective = objective,
        };
        return await SendAndParse<TunerTellResponsePayload>(request);
    }

    private async UniTask<BalanceResponsePayload> GetBestResult(string sessionId) {
        var request = new TunerBestRequestPayload {
            action = "best",
            sessionId = sessionId,
        };
        return await SendAndParse<BalanceResponsePayload>(request);
    }

    private async UniTask CloseSession(string sessionId) {
        var request = new TunerCloseRequestPayload {
            action = "close",
            sessionId = sessionId,
        };
        await SendAndParse<object>(request);
    }

    private async UniTask<T> SendAndParse<T>(object payload) where T : class {
        string requestJson = JsonUtility.ToJson(payload);
        string responseJson = await SendToPythonAsync(requestJson);
        if (string.IsNullOrWhiteSpace(responseJson)) {
            return null;
        }

        return JsonUtility.FromJson<T>(responseJson);
    }

    private float EvaluateObjectiveWithSimulation(SpeciesTuning[] tunings, int years) {
        SpeciesTuningRegistry.Apply(tunings);

        var eval = new Simulation()
            .SetLandscape(bakedLandscape)
            .AddEventGenerator(new BaseEventGenerator());

        if (speciesSetups != null) {
            for (int i = 0; i < speciesSetups.Count; i++) {
                var setup = speciesSetups[i];
                eval.AddSpecies(setup.descriptor, setup.initialPoints);
            }
        }

        eval.Run(Mathf.Max(1, years));

        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        int total = 0;
        foreach (var point in eval.GetPointsView()) {
            string species = point.descriptor.GetType().Name;
            if (!counts.ContainsKey(species)) {
                counts[species] = 0;
            }

            counts[species]++;
            total++;
        }

        var target = new Dictionary<string, float>(StringComparer.Ordinal);
        if (targetDistribution != null) {
            for (int i = 0; i < targetDistribution.Length; i++) {
                if (string.IsNullOrWhiteSpace(targetDistribution[i].speciesName)) {
                    continue;
                }

                target[targetDistribution[i].speciesName] = Mathf.Clamp01(targetDistribution[i].targetShare);
            }
        }

        float targetSum = target.Values.Sum();
        if (targetSum > 0f) {
            var keys = target.Keys.ToArray();
            for (int i = 0; i < keys.Length; i++) {
                target[keys[i]] /= targetSum;
            }
        }

        var allSpecies = new HashSet<string>(counts.Keys, StringComparer.Ordinal);
        foreach (var key in target.Keys) {
            allSpecies.Add(key);
        }

        float speciesDistributionLoss = 0f;
        foreach (var species in allSpecies) {
            target.TryGetValue(species, out float t);
            counts.TryGetValue(species, out int c);

            float share = total > 0 ? c / (float)total : 0f;
            float w = 1f + t * 2f;
            float diff = share - t;
            speciesDistributionLoss += w * diff * diff;

            if (t > 0.05f && c == 0) {
                speciesDistributionLoss += 1.5f;
            }
        }

        int baselineTreeCount = 0;
        if (speciesSetups != null) {
            for (int i = 0; i < speciesSetups.Count; i++) {
                baselineTreeCount += speciesSetups[i].initialPoints != null ? speciesSetups[i].initialPoints.Length : 0;
            }
        }

        int targetTreeCount = Mathf.Max(minimumTargetTreeCount, Mathf.RoundToInt(baselineTreeCount * targetTreeCountMultiplier));
        float countRatio = targetTreeCount > 0 ? total / (float)targetTreeCount : 1f;

        float lowCountPenalty = countRatio < 1f ? Mathf.Pow(1f - countRatio, 2f) * 2.5f : 0f;
        float overcrowdingPenalty = countRatio > 2.4f ? Mathf.Pow(countRatio - 2.4f, 2f) * 0.35f : 0f;
        float abundanceReward = Mathf.Min(0.4f, Mathf.Max(0f, countRatio - 1f) * 0.18f);
        float treeCountLoss = lowCountPenalty + overcrowdingPenalty - abundanceReward;

        return speciesDistributionWeight * speciesDistributionLoss + treeCountWeight * treeCountLoss;
    }

    private BalanceRequestPayload BuildBalanceRequest() {
        var allPoints = simulation.GetPointsView().ToArray();
        int total = allPoints.Length;
        var countBySpecies = new Dictionary<string, int>(StringComparer.Ordinal);

        for (int i = 0; i < allPoints.Length; i++) {
            var point = allPoints[i];
            var speciesName = point.descriptor.GetType().Name;

            if (!countBySpecies.ContainsKey(speciesName)) {
                countBySpecies[speciesName] = 0;
            }

            countBySpecies[speciesName]++;
        }

        var targetBySpecies = new Dictionary<string, float>(StringComparer.Ordinal);
        if (targetDistribution != null) {
            for (int i = 0; i < targetDistribution.Length; i++) {
                if (string.IsNullOrWhiteSpace(targetDistribution[i].speciesName)) {
                    continue;
                }

                targetBySpecies[targetDistribution[i].speciesName] = Mathf.Clamp01(targetDistribution[i].targetShare);
            }
        }

        var allSpeciesNames = new HashSet<string>(countBySpecies.Keys, StringComparer.Ordinal);
        foreach (var key in targetBySpecies.Keys) {
            allSpeciesNames.Add(key);
        }

        var speciesPayload = new List<SpeciesBalancePayload>(allSpeciesNames.Count);
        foreach (var speciesName in allSpeciesNames) {
            countBySpecies.TryGetValue(speciesName, out int speciesCount);
            targetBySpecies.TryGetValue(speciesName, out float targetShare);

            var tuning = SpeciesTuningRegistry.Get(speciesName);
            speciesPayload.Add(new SpeciesBalancePayload {
                speciesName = speciesName,
                currentShare = total > 0 ? speciesCount / (float)total : 0f,
                targetShare = targetShare,
                growthMultiplier = tuning.growthMultiplier,
                mortalityMultiplier = tuning.mortalityMultiplier,
                seedingMultiplier = tuning.seedingMultiplier,
            });
        }

        return new BalanceRequestPayload {
            trials = balancingTrials,
            horizonYears = balancingHorizonYears,
            species = speciesPayload.ToArray(),
        };
    }

    private async UniTask<string> SendToPythonAsync(string json) {
        return await UniTask.RunOnThreadPool(() => {
            using var client = new TcpClient();
            client.ReceiveTimeout = 120000;
            client.SendTimeout = 120000;
            client.Connect(tunerHost, tunerPort);

            using NetworkStream stream = client.GetStream();
            using var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
            using var reader = new StreamReader(stream, Encoding.UTF8);

            writer.WriteLine(json);
            return reader.ReadLine();
        });
    }

    private async UniTask<bool> EnsurePythonServerRunning() {
        if (await IsServerReachable()) {
            return true;
        }

        if (!autoStartPythonServer) {
            return false;
        }

        bool shouldStart = pythonServerProcess == null || pythonServerProcess.HasExited;
        if (shouldStart) {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrWhiteSpace(projectRoot)) {
                Debug.LogError("Failed to resolve project root for Python balancer.");
                return false;
            }

            string scriptPath = Path.GetFullPath(Path.Combine(projectRoot, pythonScriptRelativePath));
            if (!File.Exists(scriptPath)) {
                Debug.LogError($"Python balancer script not found: {scriptPath}");
                return false;
            }

            var startInfo = new System.Diagnostics.ProcessStartInfo {
                FileName = pythonExecutable,
                Arguments = $"\"{scriptPath}\" --host {tunerHost} --port {tunerPort}",
                WorkingDirectory = projectRoot,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            try {
                pythonServerProcess = new System.Diagnostics.Process {
                    StartInfo = startInfo,
                    EnableRaisingEvents = true,
                };
                pythonServerProcess.OutputDataReceived += (_, e) => {
                    if (!string.IsNullOrWhiteSpace(e.Data)) {
                        Debug.Log($"[PyTuner] {e.Data}");
                    }
                };
                pythonServerProcess.ErrorDataReceived += (_, e) => {
                    if (!string.IsNullOrWhiteSpace(e.Data)) {
                        Debug.LogWarning($"[PyTuner] {e.Data}");
                    }
                };

                if (!pythonServerProcess.Start()) {
                    Debug.LogError("Failed to start Python balancer process.");
                    return false;
                }

                pythonServerStartedByRuntime = true;
                pythonServerProcess.BeginOutputReadLine();
                pythonServerProcess.BeginErrorReadLine();
            } catch (Exception ex) {
                Debug.LogError($"Failed to start Python balancer: {ex.Message}");
                return false;
            }
        }

        int waitStepMs = 200;
        int elapsedMs = 0;
        while (elapsedMs < pythonServerStartupTimeoutMs) {
            if (await IsServerReachable()) {
                return true;
            }

            await UniTask.Delay(waitStepMs);
            elapsedMs += waitStepMs;
        }

        return false;
    }

    private async UniTask<bool> IsServerReachable() {
        return await UniTask.RunOnThreadPool(() => {
            try {
                using var client = new TcpClient();
                var connectTask = client.ConnectAsync(tunerHost, tunerPort);
                bool connected = connectTask.Wait(TimeSpan.FromMilliseconds(250));
                return connected && client.Connected;
            } catch {
                return false;
            }
        });
    }

    private void OnDestroy() {
        if (!pythonServerStartedByRuntime) {
            return;
        }

        if (pythonServerProcess == null || pythonServerProcess.HasExited) {
            return;
        }

        try {
            pythonServerProcess.Kill();
        } catch (Exception ex) {
            Debug.LogWarning($"Failed to stop Python balancer process: {ex.Message}");
        }
    }

    private void DrawTrees() {
        foreach (var point in simulation.GetPointsView()) {
            float h = bakedLandscape.Height(point.position);
            if (float.IsNaN(h)) {
                continue;
            }

            var instancePosition = new Vector3(point.position.x, h, point.position.y);

            foreach (var speciesContainer in speciesContainers) {
                speciesContainer.HandlePointView(point, instancePosition);
            }
        }

        foreach (var speciesContainer in speciesContainers) {
            speciesContainer.Flush();
        }
    }

    private const int MAX_TILE_RES = 513;
    private const string TERRAIN_PARENT_NAME = "GeneratedTerrain";

    static int NextValidTerrainResolution(int minSize) {
        int res = 33;
        while (res < minSize && res < 4097) res = (res - 1) * 2 + 1;
        return Mathf.Min(res, 4097);
    }

    private void DrawLandscape() {
        GameObject parent = null;

        var heightmap = bakedLandscape.heightmap;
        int texW = heightmap.width;
        int texH = heightmap.height;
        var rawData = heightmap.GetRawTextureData<float>();

        float worldSizeX = bakedLandscape.texelSize.x * texW;
        float worldSizeZ = bakedLandscape.texelSize.y * texH;
        float worldHeight = Mathf.Max(bakedLandscape.maxHeight - bakedLandscape.minHeight, 0.01f);

        int stride = MAX_TILE_RES - 1;
        int tilesX = Mathf.CeilToInt((float)texW / stride);
        int tilesZ = Mathf.CeilToInt((float)texH / stride);

        int tileRes = NextValidTerrainResolution(MAX_TILE_RES);

        GameObject terrainParent;
        if (parent != null) {
            terrainParent = parent;
        } else {
            var existingParent = GameObject.Find(TERRAIN_PARENT_NAME);
            if (existingParent != null) {
                terrainParent = existingParent;
            } else {
                terrainParent = new GameObject(TERRAIN_PARENT_NAME);
            }
        }

        for (int i = terrainParent.transform.childCount - 1; i >= 0; i--) {
            DestroyImmediate(terrainParent.transform.GetChild(i).gameObject);
        }

        for (int tz = 0; tz < tilesZ; tz++) {
            for (int tx = 0; tx < tilesX; tx++) {
                int pixX0 = tx * stride;
                int pixZ0 = tz * stride;
                int tilePixW = Mathf.Min(MAX_TILE_RES, texW - pixX0);
                int tilePixH = Mathf.Min(MAX_TILE_RES, texH - pixZ0);

                float tileWorldW = (tilePixW - 1) * bakedLandscape.texelSize.x;
                float tileWorldH = (tilePixH - 1) * bakedLandscape.texelSize.y;

                float[,] heights = new float[tileRes, tileRes];
                for (int ly = 0; ly < tileRes; ly++) {
                    for (int lx = 0; lx < tileRes; lx++) {
                        float fu = tileRes > 1 ? (float)lx / (tileRes - 1) * (tilePixW - 1) : 0f;
                        float fv = tileRes > 1 ? (float)ly / (tileRes - 1) * (tilePixH - 1) : 0f;

                        int x0 = Mathf.Clamp(pixX0 + (int)fu, 0, texW - 1);
                        int z0 = Mathf.Clamp(pixZ0 + (int)fv, 0, texH - 1);
                        int x1 = Mathf.Clamp(x0 + 1, 0, texW - 1);
                        int z1 = Mathf.Clamp(z0 + 1, 0, texH - 1);

                        float tx0 = fu - Mathf.Floor(fu);
                        float tz0 = fv - Mathf.Floor(fv);

                        float h00 = rawData[z0 * texW + x0];
                        float h10 = rawData[z0 * texW + x1];
                        float h01 = rawData[z1 * texW + x0];
                        float h11 = rawData[z1 * texW + x1];

                        heights[ly, lx] = Mathf.Lerp(
                            Mathf.Lerp(h00, h10, tx0),
                            Mathf.Lerp(h01, h11, tx0),
                            tz0
                        );
                    }
                }

                var terrainData = new TerrainData {
                    heightmapResolution = tileRes,
                    size = new Vector3(tileWorldW, worldHeight, tileWorldH),
                };
                terrainData.SetHeights(0, 0, heights);

                var go = Terrain.CreateTerrainGameObject(terrainData);
                go.name = $"TerrainTile_{tx}_{tz}";
                go.transform.SetParent(terrainParent.transform);

                float originX = bakedLandscape.bbox.center.x - worldSizeX * 0.5f + pixX0 * bakedLandscape.texelSize.x;
                float originZ = bakedLandscape.bbox.center.z - worldSizeZ * 0.5f + pixZ0 * bakedLandscape.texelSize.y;
                go.transform.position = new Vector3(originX, bakedLandscape.minHeight, originZ);
            }
        }
    }
}



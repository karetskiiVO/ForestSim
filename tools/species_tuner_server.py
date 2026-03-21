# pyright: reportMissingImports=false
import argparse
import json
import socketserver
import threading
import uuid
from dataclasses import dataclass
from typing import Dict, List

import optuna


@dataclass
class SpeciesItem:
    species_name: str
    current_share: float
    target_share: float
    growth_multiplier: float
    mortality_multiplier: float
    seeding_multiplier: float


def clamp(x: float, low: float, high: float) -> float:
    return max(low, min(high, x))


def parse_species(items: List[dict]) -> List[SpeciesItem]:
    parsed: List[SpeciesItem] = []
    for item in items:
        parsed.append(
            SpeciesItem(
                species_name=item.get("speciesName", "UnknownSpecies"),
                current_share=float(item.get("currentShare", 0.0)),
                target_share=float(item.get("targetShare", 0.0)),
                growth_multiplier=float(item.get("growthMultiplier", 1.0)),
                mortality_multiplier=float(item.get("mortalityMultiplier", 1.0)),
                seeding_multiplier=float(item.get("seedingMultiplier", 1.0)),
            )
        )
    return parsed


@dataclass
class SessionState:
    study: optuna.study.Study
    species_names: List[str]
    max_trials: int
    asked_trials: int
    completed_trials: int
    pending_trials: Dict[int, optuna.trial.Trial]


SESSIONS: Dict[str, SessionState] = {}
SESSIONS_LOCK = threading.Lock()


def build_tunings_from_params(species_names: List[str], params: Dict[str, float]) -> List[dict]:
    tunings = []
    for species_name in species_names:
        g = clamp(float(params.get(f"{species_name}.growth", 1.0)), 0.5, 1.8)
        m = clamp(float(params.get(f"{species_name}.mortality", 1.0)), 0.5, 1.8)
        s = clamp(float(params.get(f"{species_name}.seeding", 1.0)), 0.5, 1.8)
        tunings.append(
            {
                "speciesName": species_name,
                "growthMultiplier": g,
                "mortalityMultiplier": m,
                "seedingMultiplier": s,
            }
        )
    return tunings


def build_tunings_from_trial(species_names: List[str], trial: optuna.trial.Trial) -> List[dict]:
    params: Dict[str, float] = {}
    for species_name in species_names:
        params[f"{species_name}.growth"] = trial.suggest_float(
            f"{species_name}.growth", 0.65, 1.55
        )
        params[f"{species_name}.mortality"] = trial.suggest_float(
            f"{species_name}.mortality", 0.65, 1.55
        )
        params[f"{species_name}.seeding"] = trial.suggest_float(
            f"{species_name}.seeding", 0.65, 1.55
        )

    return build_tunings_from_params(species_names, params)


def start_session(payload: dict) -> dict:
    species = parse_species(payload.get("species", []))
    if not species:
        return {"error": "No species in start request."}

    trials = int(payload.get("trials", 120))
    trials = max(1, min(trials, 2000))

    species_names = [s.species_name for s in species]
    sampler = optuna.samplers.TPESampler(seed=42)
    study = optuna.create_study(direction="minimize", sampler=sampler)

    session_id = str(uuid.uuid4())
    state = SessionState(
        study=study,
        species_names=species_names,
        max_trials=trials,
        asked_trials=0,
        completed_trials=0,
        pending_trials={},
    )

    with SESSIONS_LOCK:
        SESSIONS[session_id] = state

    return {
        "sessionId": session_id,
        "maxTrials": trials,
        "speciesCount": len(species_names),
    }


def ask_trial(payload: dict) -> dict:
    session_id = payload.get("sessionId", "")
    if not session_id:
        return {"error": "sessionId is required for ask."}

    with SESSIONS_LOCK:
        state = SESSIONS.get(session_id)

    if state is None:
        return {"error": "Unknown sessionId."}

    if state.asked_trials >= state.max_trials:
        return {"done": True}

    trial = state.study.ask()
    tunings = build_tunings_from_trial(state.species_names, trial)

    state.pending_trials[trial.number] = trial
    state.asked_trials += 1

    return {
        "done": False,
        "trialId": trial.number,
        "tunings": tunings,
        "askedTrials": state.asked_trials,
        "maxTrials": state.max_trials,
    }


def tell_trial(payload: dict) -> dict:
    session_id = payload.get("sessionId", "")
    trial_id = int(payload.get("trialId", -1))
    objective = float(payload.get("objective", 1e9))

    if not session_id:
        return {"error": "sessionId is required for tell."}

    with SESSIONS_LOCK:
        state = SESSIONS.get(session_id)

    if state is None:
        return {"error": "Unknown sessionId."}

    trial = state.pending_trials.pop(trial_id, None)
    if trial is None:
        return {"error": f"Trial {trial_id} is not pending."}

    state.study.tell(trial, objective)
    state.completed_trials += 1

    done = state.completed_trials >= state.max_trials
    return {
        "done": done,
        "completedTrials": state.completed_trials,
        "maxTrials": state.max_trials,
    }


def best_result(payload: dict) -> dict:
    session_id = payload.get("sessionId", "")
    if not session_id:
        return {"error": "sessionId is required for best."}

    with SESSIONS_LOCK:
        state = SESSIONS.get(session_id)

    if state is None:
        return {"error": "Unknown sessionId."}

    if state.completed_trials == 0:
        return {
            "tunings": build_tunings_from_params(state.species_names, {}),
            "objective": float("inf"),
            "completedTrials": 0,
        }

    best_trial = state.study.best_trial
    return {
        "tunings": build_tunings_from_params(state.species_names, best_trial.params),
        "objective": float(best_trial.value),
        "completedTrials": state.completed_trials,
    }


def close_session(payload: dict) -> dict:
    session_id = payload.get("sessionId", "")
    if not session_id:
        return {"error": "sessionId is required for close."}

    with SESSIONS_LOCK:
        removed = SESSIONS.pop(session_id, None)

    return {"closed": removed is not None}


def dispatch(payload: dict) -> dict:
    action = payload.get("action", "optimize")

    if action == "start":
        return start_session(payload)
    if action == "ask":
        return ask_trial(payload)
    if action == "tell":
        return tell_trial(payload)
    if action == "best":
        return best_result(payload)
    if action == "close":
        return close_session(payload)

    return {"error": f"Unsupported action: {action}"}


class TunerHandler(socketserver.StreamRequestHandler):
    def handle(self) -> None:
        line = self.rfile.readline().decode("utf-8").strip()
        if not line:
            return

        try:
            payload = json.loads(line)
            result = dispatch(payload)
            response = json.dumps(result, ensure_ascii=True)
        except Exception as ex:  # noqa: BLE001
            response = json.dumps({"tunings": [], "objective": 9999.0, "error": str(ex)}, ensure_ascii=True)

        self.wfile.write((response + "\n").encode("utf-8"))


def main() -> None:
    parser = argparse.ArgumentParser(description="Species balancing server for Unity RuntimeSimulation.")
    parser.add_argument("--host", default="127.0.0.1", help="Host interface")
    parser.add_argument("--port", type=int, default=5057, help="TCP port")
    args = parser.parse_args()

    with socketserver.ThreadingTCPServer((args.host, args.port), TunerHandler) as server:
        print(f"Species tuner server started on {args.host}:{args.port}")
        server.serve_forever()


if __name__ == "__main__":
    main()

from __future__ import annotations

import copy
import json
import tempfile
import unittest
from pathlib import Path

import validate_foundation as foundation


ROOT = Path(__file__).resolve().parents[1]


class WP0002GateControlTests(unittest.TestCase):
    def documents(self) -> tuple[dict, str]:
        state = json.loads(
            (ROOT / "governance" / "ratification-state.json").read_text(
                encoding="utf-8"
            )
        )
        packet = json.loads(
            (ROOT / "work-packets" / "proposed" / "WP-0002.json").read_text(
                encoding="utf-8"
            )
        )
        return state["entry_gates"]["ugly_gameplay_toy"], packet["contract_sha256"]

    def packet_document(self) -> dict:
        return json.loads(
            (ROOT / "work-packets" / "proposed" / "WP-0002.json").read_text(
                encoding="utf-8"
            )
        )

    def test_exact_wp0002_gate_controls_pass(self) -> None:
        gate, contract = self.documents()
        self.assertEqual(
            foundation.validate_wp0002_gate_controls(gate, contract),
            [],
        )

    def test_wrong_kind_issuer_resolver_or_contract_fails(self) -> None:
        gate, contract = self.documents()
        mutations = [
            ("required_receipt_kind", "packet-completion"),
            ("required_issuer_role", "trusted-integrator"),
            ("required_resolver_type", "local-git-tree"),
            ("required_subject_contract_sha256", {"WP-0002": "0" * 64}),
        ]
        for field, value in mutations:
            with self.subTest(field=field):
                candidate = copy.deepcopy(gate)
                candidate["receipt_requirements"][0][field] = value
                self.assertTrue(
                    foundation.validate_wp0002_gate_controls(candidate, contract)
                )

    def test_wrong_decision_kind_issuer_resolver_or_contract_fails(self) -> None:
        gate, contract = self.documents()
        mutations = [
            ("required_receipt_kind", "creator-authorization"),
            ("required_issuer_role", "trusted-integrator"),
            ("required_resolver_type", "local-git-tree"),
            ("required_subject_contract_sha256", {"WP-0002": "0" * 64}),
        ]
        for field, value in mutations:
            with self.subTest(field=field):
                candidate = copy.deepcopy(gate)
                candidate["decision_requirements"][0][field] = value
                self.assertTrue(
                    foundation.validate_wp0002_gate_controls(candidate, contract)
                )

    def test_missing_decision_or_mutated_status_or_claim_fails(self) -> None:
        gate, contract = self.documents()
        candidate = copy.deepcopy(gate)
        candidate["decision_requirements"] = [
            requirement
            for requirement in candidate["decision_requirements"]
            if requirement["decision_id"] != "D-0036"
        ]
        self.assertTrue(
            foundation.validate_wp0002_gate_controls(candidate, contract)
        )
        candidate = copy.deepcopy(gate)
        candidate["decision_requirements"][0]["accepted_statuses"] = ["open"]
        self.assertTrue(
            foundation.validate_wp0002_gate_controls(candidate, contract)
        )
        candidate = copy.deepcopy(gate)
        candidate["decision_requirements"][0]["allowed_claims"] = ["ANYTHING"]
        self.assertTrue(
            foundation.validate_wp0002_gate_controls(candidate, contract)
        )
        candidate = copy.deepcopy(gate)
        candidate["decision_requirements"][0]["mismatch_action"] = "silently-ignore"
        self.assertTrue(
            foundation.validate_wp0002_gate_controls(candidate, contract)
        )

    def test_city_authorization_subjects_or_claims_cannot_mutate(self) -> None:
        gate, contract = self.documents()
        candidate = copy.deepcopy(gate)
        city = next(
            requirement
            for requirement in candidate["receipt_requirements"]
            if requirement["purpose"] == "authorize-city-comparison"
        )
        city["subject_ids"] = ["WP-0002"]
        city["required_claims"] = ["ACCEPT-WP-0002"]
        self.assertTrue(
            foundation.validate_wp0002_gate_controls(candidate, contract)
        )

    def test_title_and_composition_require_distinct_own_receipts(self) -> None:
        gate, contract = self.documents()
        candidate = copy.deepcopy(gate)
        title = next(
            requirement
            for requirement in candidate["decision_requirements"]
            if requirement["decision_id"] == "D-0036"
        )
        composition = next(
            requirement
            for requirement in candidate["decision_requirements"]
            if requirement["decision_id"] == "D-0037"
        )
        title["receipt_id"] = "RR-WP0002-IDENTITY"
        composition["receipt_id"] = "RR-WP0002-IDENTITY"
        self.assertTrue(
            foundation.validate_wp0002_gate_controls(candidate, contract)
        )
        composition["receipt_id"] = "RR-WP0002-COMPOSITION"
        self.assertEqual(
            foundation.validate_wp0002_gate_controls(candidate, contract),
            [],
        )

    def test_distinct_receipt_jsons_may_share_an_authenticated_source(self) -> None:
        gate, contract = self.documents()
        candidate = copy.deepcopy(gate)
        for index, requirement in enumerate(candidate["receipt_requirements"]):
            requirement["receipt_id"] = f"RR-WP0002-{index}"
        self.assertEqual(
            foundation.validate_wp0002_gate_controls(candidate, contract),
            [],
        )
        candidate["receipt_requirements"][1]["receipt_id"] = candidate[
            "receipt_requirements"
        ][0]["receipt_id"]
        errors = foundation.validate_wp0002_gate_controls(candidate, contract)
        self.assertTrue(any("distinct receipt JSONs" in error for error in errors))

    def test_exact_package_graph_contract_passes(self) -> None:
        self.assertEqual(
            foundation.validate_wp0002_package_graph_contract(
                self.packet_document()
            ),
            [],
        )
        self.assertEqual(
            foundation.validate_wp0002_self_verification_contract(
                self.packet_document()
            ),
            [],
        )

    def test_package_manifest_write_or_weakened_oracle_fails(self) -> None:
        packet = self.packet_document()
        packet["declared_paths"].append("SimulationCore/package.json")
        self.assertTrue(foundation.validate_wp0002_package_graph_contract(packet))
        packet = self.packet_document()
        packet["declared_paths"].append(
            "Tools/Validation/validate_wp0002_package_graph.py"
        )
        self.assertTrue(foundation.validate_wp0002_package_graph_contract(packet))
        packet = self.packet_document()
        for test in packet["acceptance_tests"]:
            if test["id"] == "T-PACKAGE-GRAPH":
                test["oracle"] = "allow the package graph"
        self.assertTrue(foundation.validate_wp0002_package_graph_contract(packet))

    def test_accepted_or_active_wp0002_baseline_cannot_be_pending(self) -> None:
        for status in ("accepted", "active"):
            with self.subTest(status=status):
                packet = self.packet_document()
                packet["status"] = status
                self.assertEqual(
                    foundation.validate_wp0002_baseline_evidence_contract(packet),
                    [],
                )
                packet["baseline_evidence"][0]["uri"] = "pending://completion"
                packet["baseline_evidence"][0]["sha256"] = None
                self.assertTrue(
                    foundation.validate_wp0002_baseline_evidence_contract(packet)
                )

    @staticmethod
    def package_graph_documents() -> tuple[dict[str, bytes], dict[str, bytes]]:
        base_manifest = {"dependencies": {"existing": "1.2.3"}}
        base_lock = {
            "dependencies": {
                "existing": {
                    "version": "1.2.3",
                    "depth": 0,
                    "source": "registry",
                    "dependencies": {},
                    "url": "https://packages.unity.com",
                }
            }
        }
        candidate_manifest = copy.deepcopy(base_manifest)
        candidate_manifest["dependencies"].update(
            {
                "com.ac21.sasha.simulation-core": "file:../../SimulationCore",
                "com.ac21.sasha.save-contracts": "file:../../SaveContracts",
            }
        )
        candidate_lock = copy.deepcopy(base_lock)
        candidate_lock["dependencies"].update(
            {
                "com.ac21.sasha.simulation-core": {
                    "version": "file:../../SimulationCore",
                    "depth": 0,
                    "source": "local",
                    "dependencies": {},
                },
                "com.ac21.sasha.save-contracts": {
                    "version": "file:../../SaveContracts",
                    "depth": 0,
                    "source": "local",
                    "dependencies": {},
                },
            }
        )

        def encoded(value: object) -> bytes:
            return json.dumps(value, sort_keys=True).encode("utf-8") + b"\n"

        base = {
            "Game/Packages/manifest.json": encoded(base_manifest),
            "Game/Packages/packages-lock.json": encoded(base_lock),
            "SimulationCore/package.json": b'{"name":"simulation"}\n',
            "SaveContracts/package.json": b'{"name":"save"}\n',
        }
        candidate = {
            "Game/Packages/manifest.json": encoded(candidate_manifest),
            "Game/Packages/packages-lock.json": encoded(candidate_lock),
            "SimulationCore/package.json": base["SimulationCore/package.json"],
            "SaveContracts/package.json": base["SaveContracts/package.json"],
        }
        return base, candidate

    def test_package_graph_logic_rejects_extra_version_source_and_package_mutation(self) -> None:
        base, candidate = self.package_graph_documents()
        self.assertEqual(
            foundation.validate_wp0002_package_graph_documents(base, candidate),
            [],
        )
        mutations = []
        extra = copy.deepcopy(candidate)
        manifest = json.loads(extra["Game/Packages/manifest.json"])
        manifest["dependencies"]["another"] = "9.9.9"
        extra["Game/Packages/manifest.json"] = json.dumps(manifest).encode()
        mutations.append(extra)
        version = copy.deepcopy(candidate)
        manifest = json.loads(version["Game/Packages/manifest.json"])
        manifest["dependencies"]["existing"] = "2.0.0"
        version["Game/Packages/manifest.json"] = json.dumps(manifest).encode()
        mutations.append(version)
        source = copy.deepcopy(candidate)
        lock = json.loads(source["Game/Packages/packages-lock.json"])
        lock["dependencies"]["existing"]["source"] = "git"
        source["Game/Packages/packages-lock.json"] = json.dumps(lock).encode()
        mutations.append(source)
        package_manifest = copy.deepcopy(candidate)
        package_manifest["SimulationCore/package.json"] += b" "
        mutations.append(package_manifest)
        for mutation in mutations:
            with self.subTest(mutation=mutation):
                self.assertTrue(
                    foundation.validate_wp0002_package_graph_documents(
                        base, mutation
                    )
                )

    def test_missing_or_weakened_protected_checker_fails(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            missing = Path(temporary) / "missing.py"
            self.assertTrue(
                foundation.validate_wp0002_package_graph_checker_contract(missing)
            )
            weakened = Path(temporary) / "checker.py"
            source = foundation.WP0002_PACKAGE_GRAPH_CHECKER.read_bytes()
            weakened.write_bytes(source + b"\n# weakened candidate\n")
            self.assertTrue(
                foundation.validate_wp0002_package_graph_checker_contract(weakened)
            )

    def test_self_verification_paths_cannot_be_reopened(self) -> None:
        packet = self.packet_document()
        packet["declared_paths"].append(".github/workflows/wp0002-ci.yml")
        self.assertTrue(
            foundation.validate_wp0002_self_verification_contract(packet)
        )

    def test_save_gate_consolidation_cannot_drop_permutation_or_atomic_coverage(self) -> None:
        packet = self.packet_document()
        for test in packet["acceptance_tests"]:
            if test["id"] == "T-DEV-SAVE-ATOMIC":
                test["oracle"] = "atomic only"
        self.assertTrue(foundation.validate_wp0002_package_graph_contract(packet))
        packet = self.packet_document()
        packet["acceptance_tests"].append(
            {
                "id": "T-SAVE-PERMUTATIONS",
                "kind": "save",
                "command": "python3 Tools/ScenarioRunner/run.py SCN_SAVE_TRANSITIONS",
                "oracle": "duplicate coverage",
                "required": True,
                "scenario_id": "SCN_SAVE_TRANSITIONS",
            }
        )
        self.assertTrue(foundation.validate_wp0002_package_graph_contract(packet))

    def test_save_confinement_and_consolidated_permutation_contracts_are_pinned(self) -> None:
        packet = self.packet_document()
        self.assertEqual(
            foundation.validate_wp0002_package_graph_contract(packet),
            [],
        )
        mutations = []

        missing_boundary = copy.deepcopy(packet)
        missing_boundary["acceptance_tests"] = [
            test
            for test in missing_boundary["acceptance_tests"]
            if test["id"] != "T-DEV-SAVE-BOUNDARY"
        ]
        mutations.append(missing_boundary)

        weakened_boundary = copy.deepcopy(packet)
        next(
            test
            for test in weakened_boundary["acceptance_tests"]
            if test["id"] == "T-DEV-SAVE-BOUNDARY"
        )["oracle"] = "writes stay somewhere under persistent data"
        mutations.append(weakened_boundary)

        weakened_permutations = copy.deepcopy(packet)
        next(
            test
            for test in weakened_permutations["acceptance_tests"]
            if test["id"] == "T-PERMUTATIONS"
        )["oracle"] = "all four permutations complete"
        mutations.append(weakened_permutations)

        reopened_module_test = copy.deepcopy(packet)
        reopened_module_test["acceptance_tests"].append(
            {
                "id": "T-MODULES",
                "kind": "scenario",
                "command": "python3 Tools/ScenarioRunner/run.py SCN_PREPARATION_MODULE_MATRIX",
                "oracle": "duplicate module coverage",
                "required": True,
                "scenario_id": "SCN_PREPARATION_MODULE_MATRIX",
            }
        )
        mutations.append(reopened_module_test)

        optional_duplicate_module = copy.deepcopy(packet)
        optional_duplicate_module["acceptance_tests"].append(
            {
                "id": "T-MODULES",
                "kind": "scenario",
                "command": "python3 Tools/ScenarioRunner/run.py SCN_PREPARATION_MODULE_MATRIX",
                "oracle": "duplicate module coverage",
                "required": False,
                "scenario_id": "SCN_PREPARATION_MODULE_MATRIX",
            }
        )
        mutations.append(optional_duplicate_module)

        for mutation in mutations:
            with self.subTest(test_ids=[
                test["id"] for test in mutation["acceptance_tests"]
            ]):
                self.assertTrue(
                    foundation.validate_wp0002_package_graph_contract(mutation)
                )

    def test_wp0002_ci_save_lifecycle_cannot_be_weakened(self) -> None:
        source = (ROOT.parent.parent / ".github" / "workflows" / "wp0002-ci.yml").read_text(
            encoding="utf-8"
        )
        with tempfile.TemporaryDirectory() as temporary:
            workflow = Path(temporary) / "wp0002-ci.yml"
            workflow.write_text(source, encoding="utf-8")
            self.assertEqual(
                foundation.validate_wp0002_ci_save_contract(workflow),
                [],
            )
            mutations = (
                source.replace(
                    "foundation.wp0002_ci_requires_lastbearing_project(",
                    "foundation.untrusted_lastbearing_policy(",
                ),
                source.replace(
                    'if [[ -e "$path" || -L "$path" ]]; then',
                    'if [[ -d "$path" ]]; then',
                ),
                source.replace(
                    'dotnet run --project "$project" --configuration Release -- --test dev-save-boundary',
                    'echo "boundary skipped"',
                ),
                source.replace(
                    'elif [[ "$status" == "proposed" || "$status" == "accepted" || "$status" == "active" || "$status" == "rejected" || "$status" == "superseded" || "$status" == "rolled-back" ]]; then',
                    'else',
                ),
                source.replace('--test dev-save-atomic', '--test all'),
            ) + tuple(
                source.replace(
                    command.replace(
                        "Tests/AtomicLandPirate.CoreTests/LastBearing/AtomicLandPirate.LastBearingTests.csproj",
                        '"$project"',
                    ),
                    'echo "required command skipped"',
                    1,
                )
                for command in foundation.WP0002_REQUIRED_CI_COMMANDS
            )
            for index, mutation in enumerate(mutations):
                with self.subTest(index=index):
                    workflow.write_text(mutation, encoding="utf-8")
                    self.assertTrue(
                        foundation.validate_wp0002_ci_save_contract(workflow)
                    )

    def test_wp0002_packet_and_workflow_command_matrix_are_exact(self) -> None:
        workflow = ROOT.parent.parent / ".github" / "workflows" / "wp0002-ci.yml"
        packet = self.packet_document()
        self.assertEqual(
            foundation.validate_wp0002_ci_save_contract(workflow, packet), []
        )
        for test_id in foundation.WP0002_REQUIRED_CI_TEST_IDS:
            with self.subTest(test_id=test_id):
                candidate = copy.deepcopy(packet)
                next(
                    test
                    for test in candidate["acceptance_tests"]
                    if test["id"] == test_id
                )["command"] = "echo weakened"
                self.assertTrue(
                    foundation.validate_wp0002_ci_save_contract(
                        workflow, candidate
                    )
                )


if __name__ == "__main__":
    unittest.main()

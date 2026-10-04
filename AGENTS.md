# Repository instructions

Read [requirements instructions](docs/requirements/AGENT_INSTRUCTIONS.md),
[human intervention protocol](docs/requirements/HUMAN_INTERVENTION.md),
[questions](docs/requirements/QUESTIONS.md), and
[milestone plan](docs/requirements/25_IMPLEMENTATION_PLAN.md) before implementation.
Maintain docs/STATUS.md and docs/DECISIONS.md. Preserve apps/unity and all existing asset GUIDs.
Read docs/MEMORY.md on resumption and update it when implementation state, owner decisions,
verification limits or next steps change. This is project handoff memory, not user data.

Mobile app and Android/iOS builds must remain portrait-only. Preserve the user's Unity
Editor window layout, docking, sizing and Game-view selection. Do not invoke portrait
preview helpers or rearrange windows automatically; they are explicit user menu actions.
Prefer isolated batch build snapshots under ignored artifacts/ to changing the interactive
Editor's build target/layout. Preserve the original project and its source asset GUIDs.

## Blender preference (preserved from workspace instructions)
For Blender modeling, use the existing blender MCP in the currently open interactive process.
Inspect PID, file and scene first. Use live_workflow_status, live_workflow_submit and
live_workflow_control for short visible stages. New models require a unique fresh .blend
in the same process through live_workflow_new_file or live.new_file; checkpoint, poll,
and read the new session token first. Edits stay in the current file and checkpoint first.
Respect pause/cancel. Never silently launch background/CLI Blender or use computer-use.
If live tools are absent use execute_blender_code and codex_live_workflow, following
D:/Blender/codex_bridge/README.md and D:/Blender/AGENTS.md.

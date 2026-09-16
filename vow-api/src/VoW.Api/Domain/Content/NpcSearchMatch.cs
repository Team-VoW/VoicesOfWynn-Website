namespace VoW.Api.Domain.Content;

/// <param name="DegeneratedName">
/// The lowercase alphanumeric form. Mod clients report an NPC under this form, so it is what
/// report.npc_name holds and what a sighting has to be looked up by.
/// </param>
public sealed record NpcSearchMatch(int NpcId, string Name, string DegeneratedName);

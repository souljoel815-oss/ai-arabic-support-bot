"""Squad-invariant tests (T010).

Covers two layers (FR-019, FR-016, SC-006, SC-007):

* In-dataclass invariants (`Squad.__post_init__`): exactly 15 players, all
  unique; XI count == 11 and a subset of the 15; captain and vice in the
  XI and distinct; bank ≥ 0; 0 ≤ FTs ≤ 5.
* Composition invariants (`validate_squad_composition`): exact 2/5/5/3
  position split and the max-3-per-club cap, plus the starting-XI
  formation. These need external lookup tables, so they live in a
  validator function rather than `__post_init__`.
"""

from __future__ import annotations

import pytest

from fpl.types import Squad, validate_squad_composition


# ---------------------------------------------------------------------------
# Helpers — match the bootstrap_static.json fixture: 5 clubs, 15 players,
# 2/5/5/3 split, ≤3 per club.
# ---------------------------------------------------------------------------

def _valid_15() -> tuple[int, ...]:
    return (101, 102, 201, 202, 203, 204, 205, 301, 302, 303, 304, 305, 401, 402, 403)


def _valid_xi() -> tuple[int, ...]:
    # 1 GK + 4 DEF + 4 MID + 2 FWD + 1 GK ... wait: must total 11 with valid formation.
    # 1 GK (101) + 4 DEF (201, 203, 204, 205) + 4 MID (301, 303, 305, 304) + 2 FWD (401, 402)
    return (101, 201, 203, 204, 205, 301, 303, 305, 304, 401, 402)


def _position_lookup() -> dict[int, int]:
    return {
        101: 1, 102: 1,
        201: 2, 202: 2, 203: 2, 204: 2, 205: 2,
        301: 3, 302: 3, 303: 3, 304: 3, 305: 3,
        401: 4, 402: 4, 403: 4,
    }


def _club_lookup() -> dict[int, int]:
    # ≤3 per club: Alpha={101,201,301}, Beta={102,202,302}, Gamma={203,303,401},
    # Delta={204,304,402}, Epsilon={205,305,403}.
    return {
        101: 1, 201: 1, 301: 1,
        102: 2, 202: 2, 302: 2,
        203: 3, 303: 3, 401: 3,
        204: 4, 304: 4, 402: 4,
        205: 5, 305: 5, 403: 5,
    }


def _make_squad(**overrides) -> Squad:
    defaults = dict(
        player_ids=_valid_15(),
        starting_xi=_valid_xi(),
        captain_id=401,
        vice_captain_id=305,
        bank=1.2,
        free_transfers=1,
    )
    defaults.update(overrides)
    return Squad(**defaults)


# ---------------------------------------------------------------------------
# Dataclass-level invariants (Squad.__post_init__)
# ---------------------------------------------------------------------------

class TestSquadDataclassInvariants:
    def test_valid_squad_constructs(self):
        squad = _make_squad()
        assert len(squad.player_ids) == 15
        assert len(squad.starting_xi) == 11

    @pytest.mark.parametrize(
        "player_ids",
        [
            _valid_15()[:14],          # 14 — too few
            _valid_15() + (999,),      # 16 — too many
            (),                        # 0 — empty
        ],
    )
    def test_rejects_wrong_total_count(self, player_ids):
        with pytest.raises(ValueError, match=r"15 players"):
            _make_squad(player_ids=player_ids)

    def test_rejects_duplicate_player_ids(self):
        dup = list(_valid_15())
        dup[0] = dup[1]
        with pytest.raises(ValueError, match=r"unique"):
            _make_squad(player_ids=tuple(dup))

    def test_rejects_xi_count_not_eleven(self):
        with pytest.raises(ValueError, match=r"11 players"):
            _make_squad(starting_xi=_valid_xi()[:10])

    def test_rejects_xi_with_duplicates(self):
        dup_xi = _valid_xi()[:-1] + (_valid_xi()[0],)  # repeat first element
        with pytest.raises(ValueError, match=r"unique"):
            _make_squad(starting_xi=dup_xi)

    def test_rejects_xi_not_subset(self):
        bad_xi = _valid_xi()[:-1] + (999,)  # 999 not in 15
        with pytest.raises(ValueError, match=r"15-man squad"):
            _make_squad(starting_xi=bad_xi)

    def test_rejects_captain_not_in_xi(self):
        # 102 is on the bench in our valid layout
        with pytest.raises(ValueError, match=r"Captain.*starting XI"):
            _make_squad(captain_id=102)

    def test_rejects_vice_not_in_xi(self):
        with pytest.raises(ValueError, match=r"Vice-captain.*starting XI"):
            _make_squad(vice_captain_id=302)

    def test_rejects_captain_equals_vice(self):
        with pytest.raises(ValueError, match=r"different players"):
            _make_squad(captain_id=401, vice_captain_id=401)

    def test_rejects_negative_bank(self):
        with pytest.raises(ValueError, match=r"Bank.*[≥>=]\s*0"):
            _make_squad(bank=-0.1)

    @pytest.mark.parametrize("ft", [-1, 6, 100])
    def test_rejects_free_transfers_out_of_range(self, ft):
        with pytest.raises(ValueError, match=r"[Ff]ree transfers.*\[0, 5\]"):
            _make_squad(free_transfers=ft)

    def test_rejects_unknown_active_chip(self):
        with pytest.raises(ValueError, match=r"active chip"):
            _make_squad(active_chip="not_a_chip")

    def test_accepts_known_active_chips(self):
        for chip in ("tc", "bb", "fh", "wc"):
            _make_squad(active_chip=chip)  # should not raise


# ---------------------------------------------------------------------------
# Composition invariants (validate_squad_composition)
# ---------------------------------------------------------------------------

class TestValidateSquadComposition:
    def test_valid_squad_passes(self):
        validate_squad_composition(
            _make_squad(),
            position_by_player_id=_position_lookup(),
            club_by_player_id=_club_lookup(),
        )

    def test_rejects_bad_position_split(self):
        # Re-label 305 (MID) as a GK → split becomes 3/5/4/3, not 2/5/5/3
        bad_pos = dict(_position_lookup())
        bad_pos[305] = 1
        with pytest.raises(ValueError, match=r"2/5/5/3"):
            validate_squad_composition(
                _make_squad(),
                position_by_player_id=bad_pos,
                club_by_player_id=_club_lookup(),
            )

    def test_rejects_more_than_three_per_club(self):
        # Move 401 from club 3 to club 1 → club 1 has {101, 201, 301, 401} = 4
        bad_clubs = dict(_club_lookup())
        bad_clubs[401] = 1
        with pytest.raises(ValueError, match=r"more than 3"):
            validate_squad_composition(
                _make_squad(),
                position_by_player_id=_position_lookup(),
                club_by_player_id=bad_clubs,
            )

    def test_rejects_xi_with_no_goalkeeper(self):
        # Bench the GK by promoting bench MID 302 in its place
        bad_xi = tuple(p if p != 101 else 302 for p in _valid_xi())
        # captain 401 is still in XI; vice 305 is still in XI; all other
        # invariants pass at the dataclass level — only the formation check
        # in validate_squad_composition should trip.
        squad = _make_squad(starting_xi=bad_xi)
        with pytest.raises(ValueError, match=r"GK"):
            validate_squad_composition(
                squad,
                position_by_player_id=_position_lookup(),
                club_by_player_id=_club_lookup(),
            )

    def test_rejects_xi_with_too_few_defenders(self):
        # Build an XI with 2 DEF (below the FPL min of 3) by swapping defenders
        # 204, 205 to bench and bringing two MIDs (302, 304) up.
        bad_xi = (101, 201, 203, 301, 302, 303, 304, 305, 401, 402, 403)
        # captain 401 and vice 305 still in XI; squad still has 15 distinct players
        squad = _make_squad(starting_xi=bad_xi)
        with pytest.raises(ValueError, match=r"DEF"):
            validate_squad_composition(
                squad,
                position_by_player_id=_position_lookup(),
                club_by_player_id=_club_lookup(),
            )

    def test_rejects_xi_with_no_forward(self):
        # Replace both forwards (401, 402) with bench mid 302 and bench DEF 202.
        # captain currently 401 → must move captain to someone in XI.
        bad_xi = (101, 201, 203, 204, 205, 202, 301, 303, 305, 304, 302)
        squad = _make_squad(starting_xi=bad_xi, captain_id=303, vice_captain_id=305)
        with pytest.raises(ValueError, match=r"FWD"):
            validate_squad_composition(
                squad,
                position_by_player_id=_position_lookup(),
                club_by_player_id=_club_lookup(),
            )

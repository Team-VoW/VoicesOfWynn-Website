const $results = $('#npc-search-results');
let searchTimer;
let autocompleteTimer;

$('#npc-search').on('input', function () {
    clearTimeout(searchTimer);
    const q = this.value.trim();
    if (q.length < 3) {
        $results.html('<p>Search for a quest name or NPC name (minimum 3 characters).</p>');
        return;
    }
    searchTimer = setTimeout(function () {
        $.getJSON('/administration/npcs/search', { q }, function (rows) {
            $results.empty();
            rows.forEach(function (quest) {
                $results.append(buildSection(quest));
            });
        });
    }, 300);
});

function escHtml(str) {
    if (str == null) return '';
    return String(str)
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;');
}

function buildSection(quest) {
    let rows = '';
    quest.npc_rows.forEach(function (npc) {
        const vaCell = npc.voice_actor_id
            ? '<a href="cast/' + npc.voice_actor_id + '" class="va-name">' + escHtml(npc.voice_actor_name) + '</a>'
            : '<i>Nobody</i>';
        const removeDisabled = npc.can_remove ? '' : 'disabled title="' + escHtml(npc.remove_title) + '"';
        rows += '<tr data-npc-id="' + npc.id + '">' +
            '<td><button class="rearrange-up-btn">\uD83D\uDD3C</button><br><button class="rearrange-down-btn">\uD83D\uDD3D</button></td>' +
            '<td class="width-150"><a href="contents/npc/' + npc.id + '" class="npc-name">' + escHtml(npc.name) + '</a></td>' +
            '<td class="width-150">' + vaCell + '</td>' +
            '<td>' + npc.recordings_count + '</td>' +
            '<td><b><a id="managelink" href="administration/npcs/manage/' + npc.id + '" class="width-150">Manage</a></b></td>' +
            '<td><button class="remove-npc-from-quest-btn" ' + removeDisabled + '>Remove from quest</button></td>' +
            '</tr>';
    });

    return $(
        '<section data-quest-id="' + quest.quest_id + '" class="txt-c">' +
        '<h3 class="txt-c">' + escHtml(quest.quest_name) + '</h3>' +
        '<form class="add-npc-to-quest-form">' +
            '<div class="autocomplete-wrapper" style="display:inline-block;position:relative;">' +
                '<input type="text" class="add-npc-autocomplete" placeholder="Add NPC to this quest\u2026" autocomplete="off">' +
                '<input type="hidden" class="add-npc-id">' +
                '<div class="autocomplete-suggestions" style="display:none;position:absolute;z-index:100;background:#fff;border:1px solid #ccc;max-height:200px;overflow-y:auto;min-width:200px;"></div>' +
            '</div>' +
            '<input type="submit" value="Add">' +
        '</form>' +
        '<table class="table-npc-management">' +
            '<tr><th>Rearrange</th><th>NPC</th><th>Voice actor</th><th>Recordings</th><th>Action</th><th>Quest</th></tr>' +
            rows +
        '</table><hr class="hr-f">' +
        '</section>'
    );
}

// Autocomplete input handler
$results.on('input', '.add-npc-autocomplete', function () {
    clearTimeout(autocompleteTimer);
    const $input = $(this);
    const $wrapper = $input.closest('.autocomplete-wrapper');
    const $idField = $wrapper.find('.add-npc-id');
    const $suggestions = $wrapper.find('.autocomplete-suggestions');
    $idField.val('');
    const q = $input.val().trim();
    if (!q) {
        $suggestions.empty().hide();
        return;
    }
    autocompleteTimer = setTimeout(function () {
        $.getJSON('/administration/npcs/autocomplete', { q }, function (npcs) {
            $suggestions.empty();
            if (!npcs.length) {
                $suggestions.hide();
                return;
            }
            npcs.forEach(function (npc) {
                const label = npc.name + (npc.archived ? ' (outdated)' : '');
                $('<div class="autocomplete-item" style="padding:4px 8px;cursor:pointer;">')
                    .text(label)
                    .data('id', npc.id)
                    .appendTo($suggestions);
            });
            $suggestions.show();
        });
    }, 200);
});

$results.on('mouseenter', '.autocomplete-item', function () {
    $(this).css('background', '#eee');
}).on('mouseleave', '.autocomplete-item', function () {
    $(this).css('background', '');
});

$results.on('click', '.autocomplete-item', function () {
    const $item = $(this);
    const $wrapper = $item.closest('.autocomplete-wrapper');
    $wrapper.find('.add-npc-autocomplete').val($item.text());
    $wrapper.find('.add-npc-id').val($item.data('id'));
    $item.closest('.autocomplete-suggestions').hide();
});

$(document).on('click', function (e) {
    if (!$(e.target).closest('.autocomplete-wrapper').length) {
        $('.autocomplete-suggestions').hide();
    }
});

// Add NPC form submit
$results.on('submit', '.add-npc-to-quest-form', function (event) {
    event.preventDefault();
    const $form = $(event.target);
    const questId = $form.closest('section').attr('data-quest-id');
    const npcId = $form.find('.add-npc-id').val();
    if (!npcId) {
        alert('Select an NPC to add.');
        return;
    }
    $.ajax({
        url: '/administration/npcs/add-to-quest/' + questId + '/' + npcId,
        type: 'PUT',
        success: function () {
            $('#npc-search').trigger('input');
        },
        error: function (result, message, error) {
            alert('An error occurred: ' + error);
        }
    });
});

// Rearrange up
$results.on('click', '.rearrange-up-btn', function (event) {
    const $moveUpRow = $(event.target).closest('tr');
    const $moveDownRow = $moveUpRow.prev();
    if ($moveDownRow.attr('data-npc-id') === undefined) {
        alert("This NPC can't be moved any higher.");
        return;
    }
    const questId = $moveUpRow.closest('section').attr('data-quest-id');
    const npc1Id = $moveUpRow.attr('data-npc-id');
    const npc2Id = $moveDownRow.attr('data-npc-id');
    $.ajax({
        url: '/administration/npcs/swap/' + questId + '/' + npc1Id + '/' + npc2Id,
        type: 'PUT',
        success: function () {
            $moveDownRow.insertAfter($moveUpRow);
        },
        error: function (result, message, error) {
            alert('An error occurred: ' + error);
        }
    });
});

// Rearrange down
$results.on('click', '.rearrange-down-btn', function (event) {
    const $moveDownRow = $(event.target).closest('tr');
    const $moveUpRow = $moveDownRow.next();
    if ($moveUpRow.attr('data-npc-id') === undefined) {
        alert("This NPC can't be moved any lower.");
        return;
    }
    const questId = $moveUpRow.closest('section').attr('data-quest-id');
    const npc1Id = $moveUpRow.attr('data-npc-id');
    const npc2Id = $moveDownRow.attr('data-npc-id');
    $.ajax({
        url: '/administration/npcs/swap/' + questId + '/' + npc1Id + '/' + npc2Id,
        type: 'PUT',
        success: function () {
            $moveDownRow.insertAfter($moveUpRow);
        },
        error: function (result, message, error) {
            alert('An error occurred: ' + error);
        }
    });
});

// Remove NPC from quest
$results.on('click', '.remove-npc-from-quest-btn', function (event) {
    const $row = $(event.target).closest('tr');
    const $section = $row.closest('section');
    const questId = $section.attr('data-quest-id');
    const npcId = $row.attr('data-npc-id');
    const npcName = $row.find('.npc-name').text();
    if (!confirm('Remove "' + npcName + '" from this quest?')) {
        return;
    }
    $.ajax({
        url: '/administration/npcs/remove-from-quest/' + questId + '/' + npcId,
        type: 'DELETE',
        success: function () {
            $row.remove();
        },
        error: function (result, message, error) {
            if (result.status === 409) {
                alert("This NPC can't be removed from this quest because it has recordings linked to the quest.");
                return;
            }
            alert('An error occurred: ' + error);
        }
    });
});

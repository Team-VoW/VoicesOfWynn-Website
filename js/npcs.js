const $results = $('#npc-search-results');
let searchTimer;
let lastSearchXhr;
let allNpcs = [];
let npcsLoaded = false;

$.getJSON('/administration/npcs/list', function (npcs) {
    allNpcs = npcs;
    npcsLoaded = true;
    populateNpcSelects($results);
});

$('#npc-search').on('input', function () {
    clearTimeout(searchTimer);
    const q = this.value.trim();
    if (q.length < 3) {
        if (lastSearchXhr) {
            lastSearchXhr.abort();
            lastSearchXhr = null;
        }
        $results.html('<p>Search for a quest name or NPC name (minimum 3 characters).</p>');
        return;
    }
    searchTimer = setTimeout(function () {
        if (lastSearchXhr) {
            lastSearchXhr.abort();
        }
        const request = $.getJSON('/administration/npcs/search', { q }, function (rows) {
            if (lastSearchXhr !== request) {
                return;
            }
            $results.empty();
            rows.forEach(function (quest) {
                $results.append(buildSection(quest));
            });
            populateNpcSelects($results);
        });
        lastSearchXhr = request;
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
        const removeBtn = npc.can_remove
            ? '<button class="remove-npc-from-quest-btn">Remove from quest</button>'
            : '<button class="remove-npc-from-quest-btn-blocked">Remove from quest</button>';
        rows += '<tr data-npc-id="' + npc.id + '">' +
            '<td><button class="rearrange-up-btn">\uD83D\uDD3C</button><br><button class="rearrange-down-btn">\uD83D\uDD3D</button></td>' +
            '<td class="width-150"><a href="contents/npc/' + npc.id + '" class="npc-name">' + escHtml(npc.name) + '</a></td>' +
            '<td class="width-150">' + vaCell + '</td>' +
            '<td>' + npc.recordings_count + '</td>' +
            '<td><b><a id="managelink" href="administration/npcs/manage/' + npc.id + '" class="width-150">Manage</a></b></td>' +
            '<td>' + removeBtn + '</td>' +
            '</tr>';
    });

    return $(
        '<section data-quest-id="' + quest.quest_id + '" class="txt-c">' +
        '<h3 class="txt-c"><span class="quest-name">' + escHtml(quest.quest_name) + '</span>' +
            ' <button class="rename-quest-btn">Rename</button>' +
        '</h3>' +
        (quest.npc_rows.length === 0 ? ' <button class="delete-quest-btn">Delete quest</button>' : '') +
        '<form class="rename-quest-form" style="display:none;">' +
            '<input type="text" class="rename-quest-input" maxlength="63" value="' + escHtml(quest.quest_name) + '">' +
            '<input type="submit" value="Confirm">' +
            ' <button type="button" class="rename-quest-cancel-btn">Cancel</button>' +
        '</form>' +
        '<form class="add-npc-to-quest-form">' +
            '<select class="add-npc-id" data-selected-npc-ids="' + escHtml(quest.npc_ids.join(',')) + '">' +
                '<option value="">Loading NPCs...</option>' +
            '</select> ' +
            '<input type="submit" value="Add">' +
        '</form>' +
        '<table class="table-npc-management">' +
            '<tr><th>Rearrange</th><th>NPC</th><th>Voice actor</th><th>Recordings</th><th>Action</th><th>Quest</th></tr>' +
            rows +
        '</table><hr class="hr-f">' +
        '</section>'
    );
}

function populateNpcSelects($container) {
    if (!npcsLoaded) {
        return;
    }

    $container.find('.add-npc-id').each(function () {
        const $select = $(this);
        const selectedNpcIds = ($select.attr('data-selected-npc-ids') || '').split(',');
        $select.empty().append('<option value="">Add NPC to this quest...</option>');
        allNpcs.forEach(function (npc) {
            const label = npc.name + (npc.archived ? ' (outdated)' : '');
            const $option = $('<option>')
                .val(npc.id)
                .text(label);
            if (selectedNpcIds.includes(String(npc.id))) {
                $option.prop('disabled', true);
            }
            $select.append($option);
        });
    });
}

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

const removeBlockedDialog = document.getElementById('remove-blocked-dialog');
$('#remove-blocked-dialog-close').on('click', function () {
    removeBlockedDialog.close();
});

function showRemoveBlockedDialog(npcId) {
    $('#remove-blocked-npc-link').attr('href', '/administration/npcs/manage/' + npcId);
    removeBlockedDialog.showModal();
}

// Remove NPC from quest (blocked — has recordings)
$results.on('click', '.remove-npc-from-quest-btn-blocked', function (event) {
    const npcId = $(event.target).closest('tr').attr('data-npc-id');
    showRemoveBlockedDialog(npcId);
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
        type: 'PUT',
        success: function () {
            $row.remove();
            if ($section.find('tr[data-npc-id]').length === 0) {
                $('#npc-search').trigger('input');
            }
        },
        error: function (result, message, error) {
            if (result.status === 409) {
                showRemoveBlockedDialog(npcId);
                return;
            }
            alert('An error occurred: ' + error);
        }
    });
});

// Delete quest
$results.on('click', '.delete-quest-btn', function () {
    const $section = $(this).closest('section');
    const questName = $section.find('.quest-name').text().trim();
    if (!confirm('Delete quest "' + questName + '"?\nThis cannot be undone.')) {
        return;
    }
    const questId = $section.attr('data-quest-id');
    $.ajax({
        url: '/administration/npcs/delete-quest/' + questId,
        type: 'DELETE',
        success: function () {
            $section.remove();
        },
        error: function (result, message, error) {
            alert('An error occurred: ' + error);
        }
    });
});

// Rename quest
$results.on('click', '.rename-quest-btn', function () {
    const $section = $(this).closest('section');
    $section.find('.rename-quest-form').toggle();
});

$results.on('click', '.rename-quest-cancel-btn', function () {
    $(this).closest('.rename-quest-form').hide();
});

$results.on('submit', '.rename-quest-form', function (event) {
    event.preventDefault();
    const $form = $(this);
    const $section = $form.closest('section');
    const questId = $section.attr('data-quest-id');
    const newName = $form.find('.rename-quest-input').val().trim();
    if (!newName) return;
    $.ajax({
        url: '/administration/npcs/rename-quest/' + questId,
        type: 'PUT',
        contentType: 'application/json',
        data: JSON.stringify({ name: newName }),
        success: function () {
            $section.find('.quest-name').text(newName);
            $form.find('.rename-quest-input').val(newName);
            $form.hide();
        },
        error: function (result, message, error) {
            alert('An error occurred: ' + error);
        }
    });
});

var $moveUpRow;
var $moveDownRow;

$(".rearrange-up-btn").click(function(event) {
    $moveUpRow = $(event.target).closest('tr');
    $moveDownRow = $(event.target).closest('tr').prev();
    if ($moveDownRow.attr('data-npc-id') === undefined) {
        alert("This NPC can't be moved any higher.");
        return;
    }

    let questId = $moveUpRow.closest('section').attr('data-quest-id');
    let npc1Id = $moveUpRow.attr('data-npc-id');
    let npc2Id = $moveDownRow.attr('data-npc-id');

    $.ajax({
        url: "/administration/npcs/swap/" + questId + "/" + npc1Id + "/" + npc2Id,
        type: 'PUT',
        success: function(result, message, error) {
            $moveDownRow.insertAfter($moveUpRow);
        },
        error: function(result, message, error) {
            alert("An error ocurred: " + error);
        }
    })
});

$(".rearrange-down-btn").click(function(event) {
    $moveDownRow = $(event.target).closest('tr');
    $moveUpRow = $(event.target).closest('tr').next();
    if ($moveUpRow.attr('data-npc-id') === undefined) {
        alert("This NPC can't be moved any lower.");
        return;
    }

    let questId = $moveUpRow.closest('section').attr('data-quest-id');
    let npc1Id = $moveUpRow.attr('data-npc-id');
    let npc2Id = $moveDownRow.attr('data-npc-id');

    $.ajax({
        url: "/administration/npcs/swap/" + questId + "/" + npc1Id + "/" + npc2Id,
        type: 'PUT',
        success: function(result, message, error) {
            $moveDownRow.insertAfter($moveUpRow);
        },
        error: function(result, message, error) {
            alert("An error ocurred: " + error);
        }
    })
});

$(".add-npc-to-quest-form").on('submit', function(event) {
    event.preventDefault();

    let $form = $(event.target);
    let questId = $form.closest('section').attr('data-quest-id');
    let npcId = $form.find('select[name="npc_id"]').val();

    if (!npcId) {
        alert("Select an NPC to add.");
        return;
    }

    $.ajax({
        url: "/administration/npcs/add-to-quest/" + questId + "/" + npcId,
        type: 'PUT',
        success: function() {
            window.location.reload();
        },
        error: function(result, message, error) {
            alert("An error occurred: " + error);
        }
    });
});

$(".remove-npc-from-quest-btn").on('click', function(event) {
    let $row = $(event.target).closest('tr');
    let $section = $row.closest('section');
    let questId = $section.attr('data-quest-id');
    let npcId = $row.attr('data-npc-id');
    let npcName = $row.find('.npc-name').text();

    if (!confirm('Remove "' + npcName + '" from this quest?')) {
        return;
    }

    $.ajax({
        url: "/administration/npcs/remove-from-quest/" + questId + "/" + npcId,
        type: 'DELETE',
        success: function() {
            $section.find('.add-npc-to-quest-select option[value="' + npcId + '"]').prop('disabled', false);
            $row.remove();
        },
        error: function(result, message, error) {
            if (result.status === 409) {
                alert("This NPC can't be removed from this quest because it has recordings linked to the quest.");
                return;
            }
            alert("An error occurred: " + error);
        }
    });
});

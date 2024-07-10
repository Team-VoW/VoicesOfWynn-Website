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
        url: "administration/npcs/swap/" + questId + "/" + npc1Id + "/" + npc2Id,
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
        url: "administration/npcs/swap/" + questId + "/" + npc1Id + "/" + npc2Id,
        type: 'PUT',
        success: function(result, message, error) {
            $moveDownRow.insertAfter($moveUpRow);
        },
        error: function(result, message, error) {
            alert("An error ocurred: " + error);
        }
    })
});

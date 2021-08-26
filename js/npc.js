var voiceActorName;
var voiceActorAvatar;
// var npcId; - filled by PHP in the view

//------------------------------CHANGING VOICE ACTORS SECTION------------------------------

function toggleRecastingButton()
{
    $("#change-actor-btn").text($("#change-actor-btn").text() === "Cancel" ? "Change" : "Cancel");
    $("#voice-actor-form").toggle();
}

$("#change-actor-btn").on('click', toggleRecastingButton);

$("#voice-actor-form").on('submit', function(event) {
    event.preventDefault();
    voiceActorName = $(event.target).find('select option:selected').text();
    voiceActorAvatar = $(event.target).find('select option:selected').attr('data-avatar-link');
    $.ajax({
        url: "administration/npcs/manage/" + npcId + "/recast/" + $(event.target).find("select").val(),
        type: 'PUT',
        success: function(result, message) {
            $("#voice-actor-avatar").attr('src', 'dynamic/avatars/' + voiceActorAvatar);
            $("#voice-actor-name").text(voiceActorName);
            toggleRecastingButton();
        },
        error: function(result, message, error) {
            alert("An error occurred: " + error);
        }
    });
});

//------------------------------DELETING RECORDINGS SECTION------------------------------

var $deletingRecording; //HTML <tr> element with recording that is being deleted

$(".delete-recording-btn").on('click', function(event){
    if (!confirm("Do you really want to delete this recording?")) {
        return;
    }
    $deletingRecording = $(event.target).closest("tr");
    $.ajax({
        url: "administration/npcs/manage/" + npcId + "/delete/" + $(event.target).attr("data-recording-id"),
        type: 'DELETE',
        success: function(result, message) {
            $deletingRecording.remove();
        },
        error: function(result, message, error) {
            alert("An error occurred: " + error);
            $deletingRecording = undefined;
        }
    });
});

//------------------------------UPLOADING RECORDINGS SECTION------------------------------

var recordingItemHtml = '<tr class="new-recording-item"><td><input name="recording{NUM}" type="file" accept="application/ogg" class="recording-input" required/></td><td><input name="line{NUM}" type="number" min="1" max="32767" value="{LINE}" class="line-input" required/></td></tr>';

function toggleAddingButton(event)
{
    $(event.target).closest('.new-recording-form').find('.add-more-recordings-button').toggle();
}

$(".recording-input").on('change', toggleAddingButton);

$(".add-more-recordings-button").on('click', function(event) {
    event.preventDefault();
    let item = recordingItemHtml;
    let num = $(event.target).parent().find('.new-recording-items-container').children().length; //Header row included - we start from 1 anyway
    num++;
    let line = $(event.target).parent().find('.new-recording-items-container').children().last().find('.line-input').val();
    line++;
    item = item.replace(/{NUM}/g, num);
    item = item.replace(/{LINE}/g, line);
    $(event.target).parent().find('.new-recording-items-container').children().last().find('.recording-input').off('change');
    $(event.target).parent().find('.new-recording-items-container').append(item);
    $(event.target).parent().find('.new-recording-items-container').children().last().find('.recording-input').on('change', toggleAddingButton);
    $(event.target).hide();
});

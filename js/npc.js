var voiceActorId;
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
    if (!confirm('Are you sure you want to recast this character?\n' +
        '\n' +
        'If there are any recordings by the original voice actor, you should archive this NPC instead.\n' +
        'Archiving an NPC will create a new one without any recordings and replace this one with it.\n' +
        'You\'ll be able to upload new recordings without deleting the current ones and nothing will be lost.\n' +
        '\n' +
        'Recasting should be done only in the case of the original voice actor not submitting their lines or not getting their voice in the mod for other reasons.')) {
        return;
    }
    voiceActorId = $(event.target).find("select").val();
    voiceActorName = $(event.target).find('select option:selected').text();
    voiceActorAvatar = $(event.target).find('select option:selected').attr('data-avatar-link');
    $.ajax({
        url: "administration/npcs/manage/" + npcId + "/recast/" + voiceActorId,
        type: 'PUT',
        success: function(result, message) {
            $("#voice-actor-avatar").attr('src', voiceActorAvatar);
            $("#voice-actor-name").text(voiceActorName);
            $(".valink").attr("href", "cast/" + voiceActorId)
            toggleRecastingButton();
        },
        error: function(result, message, error) {
            alert("An error occurred: " + error);
        }
    });
});

//-----------------------------------ARCHIVING SECTION-----------------------------------

$("#archive-btn").on('click', function() {
    if (!confirm('Are you sure you want to archive this NPC?\n' +
        'THIS IS A DESTRUCTIVE ACTION WHICH CANNOT BE UNDONE WITHOUT WEBMASTER\'S ASSISTANCE\n' +
        '\n' +
        'Archiving an NPC will unlink it from its quest and so this webpage will no longer be accessible by browsing the "Mod contents" page.\n' +
        'All recording files of this NPC will be renamed on the server to indicate, that they belong to an archived NPC.\n' +
        'At the same time, a new NPC object will be created, with the same name and profile picture, but without any recordings or voice actor.\n' +
        'This "empty" NPC will then replace this one and will be accessible on the "Mod contents" page.\n' +
        '\n' +
        'This option should be used only in case the quest got a rework and this NPC has been severely altered or removed, or in case of an recast.'
    )) {
        return;
    }

    $.ajax({
        url: "/administration/npcs/manage/" + npcId + "/archive",
        type: 'PUT',
        success: function(result, message) { /* Shouldn't happen */ },
        error: function(result, message, error) {
            if (error === "See Other") {
                //Actually success
                if (confirm('This NPC has successfully been archived and a new one was created.\n\nWould you like to go to the new NPC\'s administration page?')) {
                    window.location = JSON.parse(result.responseText).Location;
                }
                return;
            }

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

var $archivingRecordings;

$(".archive-all-recordings-btn").on('click', function(event) {
    if (!confirm('Are you sure you want to archive all recordings of this NPC in this quest?\n' +
        'THIS IS A DESTRUCTIVE ACTION WHICH CANNOT BE UNDONE WITHOUT WEBMASTER\'S ASSISTANCE\n' +
        '\n' +
        'All the recordings listed above will be hidden from public view, but will still be accessible by entering a direct link.\n' +
        'No comments will be lost and the files on the server will be renamed to indicate that they contain an archived recording.\n' +
        '\n' +
        'This option should be used only in the case the dialogue receives a large revamp and is revoiced by the original voice actor.')) {
        return;
    }

    let questId = $(event.target).attr('data-quest-id');
    $archivingRecordings = $(event.target).closest('.table-center').find('tr:not(.recordings-table-quest-header)');
    $.ajax({
        url: "administration/npcs/manage/" + npcId + "/archive-quest-recordings/" + questId,
        type: 'PUT',
        success: function(result, message) {
            $archivingRecordings.remove();
        },
        error: function(result, message, error) {
            alert("An error occurred: " + error);
            $archivingRecordings = undefined;
        }
    });
});

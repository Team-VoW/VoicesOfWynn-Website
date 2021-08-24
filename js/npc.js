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
    console.log(num);
    num++;
    console.log(num);
    let line = $(event.target).parent().find('.new-recording-items-container').children().last().find('.line-input').val();
    line++;
    item = item.replace(/{NUM}/g, num);
    item = item.replace(/{LINE}/g, line);
    $(event.target).parent().find('.new-recording-items-container').children().last().find('.recording-input').off('change');
    $(event.target).parent().find('.new-recording-items-container').append(item);
    $(event.target).parent().find('.new-recording-items-container').children().last().find('.recording-input').on('change', toggleAddingButton);
    $(event.target).hide();
});

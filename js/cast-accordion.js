$(document).ready(function () {
    $('.cast-npc-toggle').on('click', function () {
        const $block = $(this).closest('.cast-npc-block');
        const $recordings = $block.find('.cast-npc-recordings');
        const isOpen = $block.hasClass('is-open');

        $block.toggleClass('is-open', !isOpen);
        $(this).attr('aria-expanded', String(!isOpen));

        if (isOpen) {
            $recordings.slideUp(250, function () {
                $(this).css('display', 'none');
            });
        } else {
            $recordings.css('display', 'grid').hide().slideDown(250);
        }
    });
});

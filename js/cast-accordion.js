$(document).ready(function () {
    $('.cast-npc-recordings').hide();

    function toggleAccordion($header) {
        const $block = $header.closest('.cast-npc-block');
        const $recordings = $block.find('.cast-npc-recordings');
        const isOpen = $block.hasClass('is-open');

        $block.toggleClass('is-open', !isOpen);
        $header.attr('aria-expanded', String(!isOpen));
        if (isOpen) {
            $recordings.slideUp(250);
        } else {
            $recordings.slideDown(250);
        }
    }

    $('.cast-npc-header').on('click', function (e) {
        if ($(e.target).closest('.cast-npc-actions').length) return;
        toggleAccordion($(this));
    });

    $('.cast-npc-header').on('keydown', function (e) {
        // Without this guard, Space/Enter on the vote/comment action buttons bubble
        // up to .cast-npc-header and incorrectly toggle the accordion as well
        if (e.target !== this) return;
        if (e.key === 'Enter' || e.key === ' ') {
            e.preventDefault();
            toggleAccordion($(this));
        }
    });
});

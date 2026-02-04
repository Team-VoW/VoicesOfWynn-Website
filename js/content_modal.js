$(function () {
    let $modal = $('#content-modal')
    let $closeButton = $modal.find('.modal__close')
    let $content = $modal.find('#content')
    let $questBoxes = $(".quests-grid").find('.quest-card')
    let lastFocusedElement = null

    // Build NPC elements using safe DOM manipulation (OWASP GUIDELINE #3)
    // See: https://cheatsheetseries.owasp.org/cheatsheets/DOM_based_XSS_Prevention_Cheat_Sheet.html
    // Uses createElement, textContent, and appendChild which are inherently safe against XSS
    async function buildNpcElements(npcs) {
        const config = await VoWStorage.getConfig();
        const fragment = document.createDocumentFragment();

        npcs.forEach(npc => {
            const div = document.createElement('div');
            div.className = 'npc';

            const img = document.createElement('img');
            img.className = 'image';
            img.src = config.baseUrl + config.paths.npcs + npc.id + '.png';
            img.alt = 'NPC avatar';

            const p = document.createElement('p');
            p.className = 'name';

            const a = document.createElement('a');
            a.href = 'contents/npc/' + npc.id;
            a.textContent = npc.name;  // textContent is safe - never executes code

            p.appendChild(a);
            div.appendChild(img);
            div.appendChild(p);
            fragment.appendChild(div);
        });

        return fragment;
    }

    // Get all focusable elements within the modal
    function getFocusableElements() {
        return $modal.find('a[href], button:not([disabled]), textarea:not([disabled]), input:not([disabled]), select:not([disabled]), [tabindex]:not([tabindex="-1"])').filter(':visible');
    }

    // Open modal with accessibility features
    function openModal() {
        lastFocusedElement = document.activeElement;
        $modal.css('display','flex');
        $modal.attr('aria-hidden', 'false');

        // Focus the close button when modal opens
        setTimeout(() => {
            $closeButton.focus();
        }, 100);
    }

    // Close modal with accessibility features
    function closeModal() {
        $modal.css('display','none');
        $modal.attr('aria-hidden', 'true');

        // Restore focus to the element that opened the modal
        if (lastFocusedElement) {
            lastFocusedElement.focus();
        }
    }

    // Focus trap: handle Tab and Shift+Tab
    function handleTabKey(e) {
        const focusableElements = getFocusableElements();

        // Guard against empty collection
        if (focusableElements.length === 0) {
            return;
        }

        const firstElement = focusableElements.first()[0];
        const lastElement = focusableElements.last()[0];

        if (e.shiftKey) {
            // Shift + Tab
            if (document.activeElement === firstElement) {
                e.preventDefault();
                lastElement.focus();
            }
        } else {
            // Tab
            if (document.activeElement === lastElement) {
                e.preventDefault();
                firstElement.focus();
            }
        }
    }

    // Keyboard event handler for modal
    function handleKeyDown(e) {
        // Close on Escape key
        if (e.key === 'Escape' || e.keyCode === 27) {
            closeModal();
        }

        // Handle Tab for focus trap
        if (e.key === 'Tab' || e.keyCode === 9) {
            handleTabKey(e);
        }
    }

    // Use event delegation for dynamically added search results
    $('#results_container').on('click', '.quest-card, .card.q-voice', function () {
        $.getJSON(`/api/content/quest-info?questId=${$(this).attr("data-q-id")}`, async function (data) {
            let npcs = data[0].npcs;
            $content.empty().append(await buildNpcElements(npcs));
            openModal();
        }).fail(function(jqXHR, textStatus, errorThrown) {
            console.error('Failed to load quest info:', textStatus, errorThrown);
        });
    })

    $questBoxes.click(function () {
        $.getJSON(`/api/content/quest-info?questId=${$(this).attr("data-q-id")}`, async function (data) {
            let npcs = data[0].npcs;
            $content.empty().append(await buildNpcElements(npcs));
            openModal();
        }).fail(function(jqXHR, textStatus, errorThrown) {
            console.error('Failed to load quest info:', textStatus, errorThrown);
        });
    })

    $closeButton.click(function () {
        closeModal();
    })

    // Close modal when clicking outside (on the overlay)
    $modal.click(function(e) {
        if (e.target === this) {
            closeModal();
        }
    })

    // Add keyboard event listener to document
    $(document).on('keydown', function(e) {
        if ($modal.css('display') === 'flex') {
            handleKeyDown(e);
        }
    })
})
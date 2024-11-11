<?php

namespace VoicesOfWynn\Controllers\Api;

enum ApiKey
{
    case LINE_REPORT_COLLECT;
    case LINE_REPORT_MODIFY;
    case STATISTICS_AGGREGATE;
    case DISCORD_INTEGRATION;
    case PREMIUM_AUTHENTICATION;
}

